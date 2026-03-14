using System;
using System.Collections.Generic;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Application.Kpi;

public class OutOfStockCalculator : IKpiCalculator
{
    public string Name => "Out-of-Stock Items";

    public object Calculate(
        IReadOnlyCollection<ProductStateSnapshot> snapshots, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null)
    {
        // FAIL-SOFT: Trả về thông báo thân thiện để không làm sập luồng Report
        if (startDate.HasValue || endDate.HasValue)
        {
            return "N/A (Chỉ phản ánh tồn kho hiện tại)";
        }

        int outOfStockCount = 0;

        // TỐI ƯU HIỆU NĂNG: Thay LINQ bằng foreach để triệt tiêu Allocation
        foreach (var snapshot in snapshots)
        {
            if (snapshot.CurrentStock <= 0)
            {
                outOfStockCount++;
            }
        }

        return outOfStockCount; // Chấp nhận Boxing (int -> object) do thiết kế Interface
    }
}