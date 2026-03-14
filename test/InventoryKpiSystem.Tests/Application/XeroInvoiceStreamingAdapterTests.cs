using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Infrastructure.FileProcessing; // Thay bằng namespace chứa Adapter của em
using Xunit;

namespace InventoryKpiSystem.Tests.Infrastructure;

// Implement IDisposable để xUnit tự động dọn rác (xóa file temp) sau khi test xong
public class XeroInvoiceStreamingAdapterTests : IDisposable
{
    private readonly XeroInvoiceStreamingAdapter _adapter;
    private readonly string _tempFilePath;

    public XeroInvoiceStreamingAdapterTests()
    {
        // Khởi tạo Adapter
        _adapter = new XeroInvoiceStreamingAdapter();
        
        // Tạo một file tạm an toàn của hệ điều hành (Tránh lỗi đụng độ đường dẫn)
        _tempFilePath = Path.GetTempFileName(); 
    }

    public void Dispose()
    {
        // Dọn dẹp chiến trường: Xóa file tạm sau khi test chạy xong (Passed hay Failed đều xóa)
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public async Task ParseAsync_ShouldFilterGarbage_AndRouteTypesCorrectly()
    {
        // 1. ARRANGE: Tạo mảng JSON giả lập chứa cả Vàng lẫn Cát
        string jsonPayload = @"
        {
            ""Invoices"": [
                {
                    ""Type"": ""ACCPAY"",
                    ""DateString"": ""2024-01-01T00:00:00Z"",
                    ""LineItems"": [
                        { ""ItemCode"": ""SKU-VALID-01"", ""Quantity"": 10, ""UnitAmount"": 50.0 },
                        { ""Description"": ""Phí vận chuyển"", ""Quantity"": 1, ""UnitAmount"": 15.0 }, 
                        { ""ItemCode"": """", ""Description"": ""Dịch vụ bốc vác"", ""Quantity"": 1, ""UnitAmount"": 100.0 } 
                    ]
                },
                {
                    ""Type"": ""ACCREC"",
                    ""DateString"": ""2024-01-02T00:00:00Z"",
                    ""LineItems"": [
                        { ""ItemCode"": ""SKU-VALID-02"", ""Quantity"": 5, ""UnitAmount"": 120.0 }
                    ]
                }
            ]
        }";
        
        // Ghi chuỗi JSON vào file tạm
        await File.WriteAllTextAsync(_tempFilePath, jsonPayload);

        // 2. ACT: Hút dữ liệu từ Adapter thông qua Streaming (IAsyncEnumerable)
        var parsedRecords = new List<object>();
        
        await foreach (var record in _adapter.ParseAsync(_tempFilePath, CancellationToken.None))
        {
            parsedRecords.Add(record);
        }

        // 3. ASSERT: Kiểm tra "Bộ lọc thép" và "Nhà phân phối luồng"

        // BỘ LỌC RÁC: JSON có 4 LineItems, nhưng 2 cái là Rác (không có ItemCode hoặc rỗng).
        // Kỳ vọng: Chỉ lọt qua đúng 2 record chuẩn.
        parsedRecords.Should().HaveCount(2, 
            "Vì các chi phí rác (phí vận chuyển, dịch vụ) không có ItemCode hợp lệ phải bị Adapter vứt bỏ.");

        // PHÂN LOẠI LUỒNG 1: ACCPAY -> PurchaseOrder
        var firstRecord = parsedRecords[0];
        var purchaseOrder = firstRecord.Should().BeOfType<PurchaseOrder>(
            "Hóa đơn loại ACCPAY phải được Adapter map chính xác thành PurchaseOrder").Subject;
            
        purchaseOrder.ProductId.Should().Be("SKU-VALID-01");
        purchaseOrder.QuantityPurchased.Should().Be(10);
        purchaseOrder.UnitCost.Should().Be(50.0m);

        // PHÂN LOẠI LUỒNG 2: ACCREC -> SalesInvoice
        var secondRecord = parsedRecords[1];
        var salesInvoice = secondRecord.Should().BeOfType<SalesInvoice>(
            "Hóa đơn loại ACCREC phải được Adapter map chính xác thành SalesInvoice").Subject;
            
        salesInvoice.ProductId.Should().Be("SKU-VALID-02");
        salesInvoice.QuantitySold.Should().Be(5);
        salesInvoice.UnitSellingPrice.Should().Be(120.0m);
    }
}