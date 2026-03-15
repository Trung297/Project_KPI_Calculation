using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Infrastructure.FileProcessing;

// 1. DTOs dành riêng cho Xero (Gom chung vào file này để không làm rác project)
public class XeroRootDto
{
    public List<XeroInvoiceDto>? Invoices { get; set; }
}

public class XeroInvoiceDto
{
    public string? Type { get; set; } 
    public DateTimeOffset DateString { get; set; } 
    public List<XeroLineItemDto>? LineItems { get; set; }
}

public class XeroLineItemDto
{
    // SỬA BUG: Dùng ItemCode để liên kết với ProductId thay vì LineItemID
    public string? ItemCode { get; set; } 
    public decimal Quantity { get; set; }
    public decimal UnitAmount { get; set; }
}

// 2. Class Adapter (Người phiên dịch dữ liệu Xero -> Dữ liệu Core)
public class XeroInvoiceStreamingAdapter : IAsyncFileParser<object> 
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public XeroInvoiceStreamingAdapter()
    {
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
    }

    public async IAsyncEnumerable<object> ParseAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Vẫn dùng FileStream để tiết kiệm I/O
        await using var fileStream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);

        // Lưu ý: Do Xero bọc Invoices trong 1 object ngoài cùng, ta dùng Buffered Read cho toàn root.
        // Nhờ RAM ăn có 143MB cho 885 files, cách này hoàn toàn chấp nhận được.
        var rootData = await JsonSerializer.DeserializeAsync<XeroRootDto>(
            fileStream, _jsonSerializerOptions, cancellationToken);

        if (rootData?.Invoices == null) yield break;

        // Vòng lặp Phiên dịch & Lọc dữ liệu
        foreach (var invoice in rootData.Invoices)
        {
            if (invoice.LineItems == null) continue;

            foreach (var item in invoice.LineItems)
            {
                // 🛑 BỘ LỌC THÉP: Bỏ qua số lượng <= 0, hoặc các chi phí rác không có ItemCode
                if (item.Quantity <= 0 || string.IsNullOrEmpty(item.ItemCode)) 
                    continue;

                // Định tuyến MUA (Nhập kho)
                if (invoice.Type == "ACCPAY")
                {
                    yield return new PurchaseOrder
                    {
                        ProductId = item.ItemCode, // Đã sửa map đúng ItemCode
                        QuantityPurchased = (int)item.Quantity,
                        UnitCost = item.UnitAmount,
                        PurchaseDate = invoice.DateString
                    };
                }
                // Định tuyến BÁN (Xuất kho)
                else if (invoice.Type == "ACCREC")
                {
                    yield return new SalesInvoice
                    {
                        ProductId = item.ItemCode, // Đã sửa map đúng ItemCode
                        QuantitySold = (int)item.Quantity,
                        UnitSellingPrice = item.UnitAmount,
                        InvoiceDate = invoice.DateString
                    };
                }
            }
        }
    }
}