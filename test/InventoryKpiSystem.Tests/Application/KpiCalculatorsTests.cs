using System;
using System.Collections.Generic;
using FluentAssertions;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Application.Kpi;  
using Xunit;

namespace InventoryKpiSystem.Tests.Calculators;

public class KpiCalculatorsTests
{
    // ---------------------------------------------------------
    // 1. TEST: TỔNG SỐ LƯỢNG MÃ SẢN PHẨM (Total SKUs)
    // ---------------------------------------------------------
    public class TotalSkusCalculatorTests
    {
        private readonly TotalSkusCalculator _calculator = new();

        [Fact]
        public void Calculate_WithData_ReturnsCorrectCount()
        {
            var snapshots = new List<ProductStateSnapshot>
            {
                new() { ProductId = "ITEM-01" },
                new() { ProductId = "ITEM-02" },
                new() { ProductId = "ITEM-03" }
            };

            var result = _calculator.Calculate(snapshots);
            result.Should().Be(3);
        }
    }

    // ---------------------------------------------------------
    // 2. TEST: HÀNG HẾT TRONG KHO (Out of Stock)
    // ---------------------------------------------------------
    public class OutOfStockCalculatorTests
    {
        private readonly OutOfStockCalculator _calculator = new();

        [Fact]
        public void Calculate_VariousScenarios_ReturnsCorrectOutOfStockCount()
        {
            // Món 1: Còn hàng (Nhập 10)
            var item1 = new ProductStateSnapshot { ProductId = "ITEM-01" };
            item1.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 10, PurchaseDate = DateTimeOffset.UtcNow });

            // Món 2: Hết hàng (Nhập 10, Bán 10) => Phải đếm
            var item2 = new ProductStateSnapshot { ProductId = "ITEM-02" };
            item2.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 10, PurchaseDate = DateTimeOffset.UtcNow });
            item2.UpdateWithInvoice(new SalesInvoice { QuantitySold = 10, InvoiceDate = DateTimeOffset.UtcNow });

            // Món 3: Âm kho (Nhập 5, Bán 8) => Phải đếm
            var item3 = new ProductStateSnapshot { ProductId = "ITEM-03" };
            item3.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 5, PurchaseDate = DateTimeOffset.UtcNow });
            item3.UpdateWithInvoice(new SalesInvoice { QuantitySold = 8, InvoiceDate = DateTimeOffset.UtcNow });

            var snapshots = new List<ProductStateSnapshot> { item1, item2, item3 };

            var result = _calculator.Calculate(snapshots);

            // Kỳ vọng: ITEM-02 và ITEM-03 bị đếm = 2
            result.Should().Be(2);
        }
    }

    // ---------------------------------------------------------
    // 3. TEST: TỔNG GIÁ TRỊ TỒN KHO (Stock Value)
    // ---------------------------------------------------------
    public class StockValueCalculatorTests
    {
        private readonly StockValueCalculator _calculator = new();

        [Fact]
        public void Calculate_WithFIFO_ReturnsCorrectRemainingValue()
        {
            var item = new ProductStateSnapshot { ProductId = "ITEM-01" };
            
            // Lô 1: 10 cái x $100 = $1000
            item.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 10, UnitCost = 100m, PurchaseDate = DateTimeOffset.UtcNow });
            
            // Lô 2: 5 cái x $200 = $1000
            item.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 5, UnitCost = 200m, PurchaseDate = DateTimeOffset.UtcNow });

            // Bán 12 cái (Rút hết 10 cái lô 1, và 2 cái lô 2)
            // Trong kho sẽ chỉ còn lại lô 2: 3 cái x $200
            item.UpdateWithInvoice(new SalesInvoice { QuantitySold = 12, InvoiceDate = DateTimeOffset.UtcNow });

            var snapshots = new List<ProductStateSnapshot> { item };

            var result = (decimal)_calculator.Calculate(snapshots);

            // Kỳ vọng: 3 * 200 = 600m
            result.Should().Be(600m);
        }
    }

    // ---------------------------------------------------------
    // 4. TEST: SỐ BÁN TRUNG BÌNH NGÀY (Avg Daily Sales)
    // ---------------------------------------------------------
    public class AvgDailySalesCalculatorTests
    {
        private readonly AvgDailySalesCalculator _calculator = new();

        [Fact]
        public void Calculate_ValidSales_ReturnsCorrectAverage()
        {
            var item = new ProductStateSnapshot { ProductId = "ITEM-01" };
            DateTimeOffset tenDaysAgo = DateTimeOffset.UtcNow.AddDays(-10);

            // Bán 50 cái vào 10 ngày trước
            item.UpdateWithInvoice(new SalesInvoice { QuantitySold = 50, InvoiceDate = tenDaysAgo });
            // Bán thêm 150 cái vào hôm nay
            item.UpdateWithInvoice(new SalesInvoice { QuantitySold = 150, InvoiceDate = DateTimeOffset.UtcNow });

            // Tổng bán = 200. Khoảng cách thời gian = 10 ngày => 20 món / ngày
            var snapshots = new List<ProductStateSnapshot> { item };

            var result = (double)_calculator.Calculate(snapshots);

            result.Should().BeApproximately(100.0, precision: 0.1);
        }
    }

    // ---------------------------------------------------------
    // 5. TEST: TUỔI TỒN KHO TRUNG BÌNH (Avg Inventory Age)
    // ---------------------------------------------------------
    public class AvgInventoryAgeCalculatorTests
    {
        private readonly AvgInventoryAgeCalculator _calculator = new();

        [Fact]
        public void Calculate_WithMultipleBatches_ReturnsWeightedAverageAge()
        {
            var item = new ProductStateSnapshot { ProductId = "ITEM-01" };
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // Lô 1: 10 cái, mua cách đây 20 ngày
            item.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 10, PurchaseDate = now.AddDays(-20) });
            
            // Lô 2: 10 cái, mua cách đây 10 ngày
            item.UpdateWithOrder(new PurchaseOrder { QuantityPurchased = 10, PurchaseDate = now.AddDays(-10) });

            // Bán đi 5 cái (rút từ lô 1 theo FIFO). 
            // Kho còn: 5 cái (20 ngày tuổi) và 10 cái (10 ngày tuổi)
            item.UpdateWithInvoice(new SalesInvoice { QuantitySold = 5, InvoiceDate = now });

            var snapshots = new List<ProductStateSnapshot> { item };

            var result = (double)_calculator.Calculate(snapshots);

            // Kỳ vọng: ((5 * 20 ngày) + (10 * 10 ngày)) / 15 tổng tồn kho = 200 / 15 = 13.333... ngày
            result.Should().BeApproximately(13.33, precision: 0.1);
        }
    }
}