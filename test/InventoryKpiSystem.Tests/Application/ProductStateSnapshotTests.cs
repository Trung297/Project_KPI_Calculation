using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using InventoryKpiSystem.Core.Entities;
using Xunit;

namespace InventoryKpiSystem.Tests.Entities;

public class ProductStateSnapshotTests
{
    // =========================================================================
    // TEST 1: KIỂM CHỨNG THUẬT TOÁN FIFO (First-In-First-Out)
    // =========================================================================
    [Fact]
    public void UpdateWithInvoice_ShouldDeductFromOldestBatchFirst_FollowingFifoAlgorithm()
    {
        // 1. ARRANGE (Chuẩn bị dữ liệu)
        var snapshot = new ProductStateSnapshot { ProductId = "SKU-FIFO-001" };
        var baseDate = DateTimeOffset.UtcNow;

        // Bơm Lô hàng 1 (Cũ nhất): Nhập 10 cái, giá $100, cách đây 10 ngày
        var order1 = new PurchaseOrder { QuantityPurchased = 10, UnitCost = 100m, PurchaseDate = baseDate.AddDays(-10) };
        
        // Bơm Lô hàng 2 (Mới hơn): Nhập 15 cái, giá $200, cách đây 5 ngày
        var order2 = new PurchaseOrder { QuantityPurchased = 15, UnitCost = 200m, PurchaseDate = baseDate.AddDays(-5) };
        
        // Bắn Lệnh bán hàng: Khách mua 12 cái (Kỳ vọng: Lấy sạch 10 cái lô 1, và 2 cái lô 2)
        var invoice = new SalesInvoice { QuantitySold = 12, UnitSellingPrice = 300m, InvoiceDate = baseDate };

        // 2. ACT (Thực thi)
        snapshot.UpdateWithOrder(order1);
        snapshot.UpdateWithOrder(order2);
        snapshot.UpdateWithInvoice(invoice);

        // 3. ASSERT (Kiểm chứng kết quả)
        
        // Kiểm tra State tổng quan
        snapshot.TotalPurchased.Should().Be(25, "Vì tổng nhập là 10 + 15");
        snapshot.TotalSold.Should().Be(12, "Vì khách mua 12 cái");
        snapshot.CurrentStock.Should().Be(13, "Tồn kho hiện tại phải = TotalPurchased - TotalSold");

        // Kiểm tra sâu vào "Ruột" của Queue FIFO
        snapshot.UnsoldBatches.Should().HaveCount(1, 
            "Lô hàng 1 (10 cái) đã bị bán sạch, thuật toán phải Dequeue vứt nó đi để giải phóng RAM (O(1))");

        // Kiểm tra chính xác trạng thái của Lô hàng còn lại trong Queue
        var remainingBatch = snapshot.UnsoldBatches.Peek();
        remainingBatch.RemainingQuantity.Should().Be(13, 
            "Lô 2 có 15 cái, bị rút đi 2 cái (do 10 cái đã lấy từ lô 1), nên phải còn đúng 13 cái");
        remainingBatch.UnitCost.Should().Be(200m, 
            "Đơn giá của lô hàng 2 ($200) phải được giữ nguyên vẹn, không bị nhầm lẫn");
    }

    // =========================================================================
    // TEST 2: KIỂM CHỨNG BẢO TOÀN DỮ LIỆU (Immutability & Encapsulation)
    // =========================================================================
    [Fact]
    public void ProductStateSnapshot_ShouldMaintainImmutability_AndPreventExternalModification()
    {
        // 1. ARRANGE
        var type = typeof(ProductStateSnapshot);

        // 2 & 3. ACT & ASSERT bằng Reflection (Phát hiện những kẻ phá vỡ SOLID)

        // Phép thử A: CurrentStock tuyệt đối không được phép có hàm Set (Phải là Computed Property)
        var currentStockProp = type.GetProperty(nameof(ProductStateSnapshot.CurrentStock));
        currentStockProp.Should().NotBeNull();
        currentStockProp!.CanWrite.Should().BeFalse(
            "CurrentStock phải là Computed Property (=>). Nếu ai đó thêm 'set' vào, dữ liệu sẽ bị thao túng.");

        // Phép thử B: TotalPurchased phải bị khóa (private set)
        var totalPurchasedProp = type.GetProperty(nameof(ProductStateSnapshot.TotalPurchased));
        totalPurchasedProp.Should().NotBeNull();
        totalPurchasedProp!.GetSetMethod(nonPublic: true).Should().NotBeNull(
            "Entity cần 'private set' để tự cập nhật trạng thái bên trong 2 hàm nghiệp vụ.");
        totalPurchasedProp.GetSetMethod(nonPublic: false).Should().BeNull(
            "Tuyệt đối KHÔNG được mở 'public set' cho TotalPurchased. Vi phạm tính đóng gói (Encapsulation) của DDD.");

        // Phép thử C: TotalSold cũng phải bị khóa (private set)
        var totalSoldProp = type.GetProperty(nameof(ProductStateSnapshot.TotalSold));
        totalSoldProp.Should().NotBeNull();
        totalSoldProp!.GetSetMethod(nonPublic: true).Should().NotBeNull();
        totalSoldProp.GetSetMethod(nonPublic: false).Should().BeNull(
            "Tuyệt đối KHÔNG được mở 'public set' cho TotalSold.");

        // Phép thử D: Đảm bảo dữ liệu chỉ đổi khi đi qua "Cửa chính"
        var snapshot = new ProductStateSnapshot { ProductId = "SKU-SAFE-001" };
        snapshot.CurrentStock.Should().Be(0, "Kho khởi tạo mặc định phải bằng 0");

        snapshot.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 99, PurchaseDate = DateTimeOffset.UtcNow });
        snapshot.CurrentStock.Should().Be(99, "Dữ liệu chỉ được phép tăng lên khi gọi hàm UpdateWithOrder");
        
        // Không có bất kỳ cách nào để gõ: snapshot.CurrentStock = 100; (Trình biên dịch sẽ báo lỗi ngay lập tức)
    }
}