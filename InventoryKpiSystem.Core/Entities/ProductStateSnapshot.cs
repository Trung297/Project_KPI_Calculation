using System;
using System.Collections.Generic;

namespace InventoryKpiSystem.Core.Entities;

// BẮT BUỘC dùng class để tránh lỗi copy value type khi dùng TryPeek/TryGetValue
public class PurchaseBatch
{
    public DateTimeOffset PurchaseDate { get; set; }
    public int RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class DailyMetric
{
    public int QuantitySold { get; set; }
    public int QuantityPurchased { get; set; }
    public decimal TotalSalesValue { get; set; }
}

public class ProductStateSnapshot
{
    public required string ProductId { get; init; }

    public int TotalPurchased { get; private set; }
    public int TotalSold { get; private set; }
    public int CurrentStock => TotalPurchased - TotalSold;

    // Collection tiêu chuẩn (Nhanh, nhẹ, không tốn chi phí quản lý lock ngầm)
    public Queue<PurchaseBatch> UnsoldBatches { get; } = new();
    public Dictionary<DateOnly, DailyMetric> DailyMetrics { get; } = new();

    // 🛡️ "Ổ khóa" bảo vệ toàn vẹn dữ liệu trong môi trường Multi-threaded
    // Đảm bảo không có 2 luồng (từ 2 file báo cáo) cùng lúc thay đổi số lượng tồn kho
    private readonly object _stateLock = new object();

    // ==========================================
    // CÁC HÀM CẬP NHẬT TRẠNG THÁI 
    // ==========================================

    public void UpdateWithOrder(in PurchaseOrder order) 
    {
        // Xếp hàng: Luồng nào cầm chìa khóa mới được vào thao tác mảng và biến
        lock (_stateLock)
        {
            TotalPurchased += order.QuantityPurchased;
            
            UnsoldBatches.Enqueue(new PurchaseBatch 
            {
                PurchaseDate = order.PurchaseDate, 
                RemainingQuantity = order.QuantityPurchased,
                UnitCost = order.UnitCost
            });

            var orderDate = DateOnly.FromDateTime(order.PurchaseDate.UtcDateTime);
            if (!DailyMetrics.TryGetValue(orderDate, out var metric))
            {
                metric = new DailyMetric();
                DailyMetrics[orderDate] = metric;
            }
            metric.QuantityPurchased += order.QuantityPurchased;
        }
    }

    public void UpdateWithInvoice(in SalesInvoice invoice) 
    {
        // Bảo vệ thuật toán rút ruột FIFO khỏi Race Condition
        lock (_stateLock) 
        {
            TotalSold += invoice.QuantitySold;

            // THUẬT TOÁN FIFO NGUYÊN BẢN
            int quantityToDeduct = invoice.QuantitySold;
            
            while (quantityToDeduct > 0 && UnsoldBatches.TryPeek(out var oldestBatch))
            {
                if (oldestBatch.RemainingQuantity <= quantityToDeduct)
                {
                    quantityToDeduct -= oldestBatch.RemainingQuantity;
                    UnsoldBatches.Dequeue(); // Vứt bỏ lô đã hết
                }
                else
                {
                    oldestBatch.RemainingQuantity -= quantityToDeduct; // Trừ trực tiếp trên Reference Type
                    quantityToDeduct = 0; 
                }
            }

            var invoiceDate = DateOnly.FromDateTime(invoice.InvoiceDate.UtcDateTime);
            var salesValue = invoice.QuantitySold * invoice.UnitSellingPrice;

            if (!DailyMetrics.TryGetValue(invoiceDate, out var metric))
            {
                metric = new DailyMetric();
                DailyMetrics[invoiceDate] = metric;
            }
            metric.QuantitySold += invoice.QuantitySold;
            metric.TotalSalesValue += salesValue;
        }
    }
}