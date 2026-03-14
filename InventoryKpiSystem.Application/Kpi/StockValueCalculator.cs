using System;
using System.Collections.Generic;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Application.Kpi;

/// <summary>
/// Strategy tính toán Tổng giá trị Hàng tồn kho (Cost of Inventory / Stock Value).
/// </summary>
public class StockValueCalculator : IKpiCalculator
{
    // Tên KPI chuẩn xác theo tài liệu nghiệp vụ
    public string Name => "Cost of Inventory (Stock Value)";

    public object Calculate(
        IReadOnlyCollection<ProductStateSnapshot> snapshots, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null)
    {
        decimal totalStockValue = 0m;

        // TỐI ƯU RAM (Senior Level): 
        // Thay vì dùng LINQ (snapshots.SelectMany(...).Sum(...)) gây cấp phát bộ nhớ ngầm (Closure/Delegate allocation),
        // chúng ta dùng vòng lặp foreach lồng nhau thuần túy. 
        // Với hàng trăm ngàn record, cách này chạy cực nhanh và Garbage Collector (GC) không phải dọn rác.
        foreach (var snapshot in snapshots)
        {
            // Duyệt qua từng lô hàng còn tồn đọng trong Queue FIFO
            foreach (var batch in snapshot.UnsoldBatches)
            {
                // Hỗ trợ tính năng Bonus: Date-range filtering (Lọc theo ngày)
                if (startDate.HasValue && batch.PurchaseDate < startDate.Value) 
                    continue;
                
                if (endDate.HasValue && batch.PurchaseDate > endDate.Value) 
                    continue;

                // Áp dụng chuẩn công thức: Stock Value = Σ (Unsold Quantity * Unit Cost)
                totalStockValue += batch.RemainingQuantity * batch.UnitCost;
            }
        }

        // Trả về định dạng tiền tệ cơ bản, việc format hiển thị (ToString("C")) sẽ nhường cho tầng UI/Console
        return totalStockValue;
    }
}