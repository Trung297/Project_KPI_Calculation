using System;
using System.Collections.Generic;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Application.Kpi;

public class AvgInventoryAgeCalculator : IKpiCalculator
{
    public string Name => "Average Inventory Age"; 

    public object Calculate(
        IReadOnlyCollection<ProductStateSnapshot> snapshots, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null)
    {
        // 1. RÀO CHẮN FAIL-SOFT (Đã tháo bom)
        // Trả về một chuỗi string thân thiện thay vì ném Exception để không làm crash vòng lặp Strategy
        if (startDate.HasValue || endDate.HasValue)
        {
            return "N/A (Chỉ phản ánh tồn kho hiện hành)";
        }

        var calculateAt = DateTimeOffset.UtcNow; 
        
        double totalAgeInDays = 0;
        long totalRemainingQuantity = 0;

        // 2. DUYỆT DỮ LIỆU TỪ KHO IN-MEMORY
        foreach (var snapshot in snapshots)
        {
            foreach (var batch in snapshot.UnsoldBatches)
            {
                if (batch.RemainingQuantity > 0)
                {
                    var ageDays = (calculateAt - batch.PurchaseDate).TotalDays;
                    
                    // Xử lý dữ liệu ảo: Nếu file JSON chứa ngày nhập ở tương lai, ta set min = 0 ngày
                    ageDays = Math.Max(0, ageDays); 

                    // Tính trung bình gia quyền
                    totalAgeInDays += ageDays * batch.RemainingQuantity;
                    totalRemainingQuantity += batch.RemainingQuantity;
                }
            }
        }

        // 3. XỬ LÝ BIÊN
        if (totalRemainingQuantity == 0) 
            return 0.0;

        // 4. TRẢ VỀ KẾT QUẢ
        return Math.Round(totalAgeInDays / totalRemainingQuantity, 2);
    }
}