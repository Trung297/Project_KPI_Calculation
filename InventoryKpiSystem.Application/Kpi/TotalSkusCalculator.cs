using System;
using System.Collections.Generic;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Application.Kpi;

public class TotalSkusCalculator : IKpiCalculator
{
    public string Name => "Total SKUs";

    public object Calculate(
        IReadOnlyCollection<ProductStateSnapshot> snapshots, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null)
    {
        // FAIL-SOFT: Trả về thông báo thân thiện thay vì Exception
        if (startDate.HasValue || endDate.HasValue)
        {
            return "N/A (Chỉ phản ánh tồn kho hiện tại)";
        }

        // Lấy Count property có độ phức tạp là $O(1)$, tốc độ cực kỳ nhanh
        return snapshots.Count; 
    }
}