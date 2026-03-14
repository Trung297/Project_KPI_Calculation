using System;
using System.Collections.Generic;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Application.Kpi;

/// <summary>
/// Strategy tính toán Số lượng bán trung bình mỗi ngày (Average Daily Sales).
/// Công thức: Total Quantity Sold / Number of Sales Days
/// </summary>
public class AvgDailySalesCalculator : IKpiCalculator
{
    // Tên KPI chuẩn xác theo tài liệu nghiệp vụ
    public string Name => "Average Daily Sales";

    public object Calculate(
        IReadOnlyCollection<ProductStateSnapshot> snapshots, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null)
    {
        long totalQuantitySold = 0;
        
        // TỐI ƯU RAM & LOGIC: 
        // Dùng HashSet<DateOnly> để tự động loại bỏ các ngày trùng lặp giữa nhiều sản phẩm.
        // Cấu trúc dữ liệu này cực kỳ tối ưu cho việc đếm phần tử duy nhất (Distinct) với độ phức tạp O(1) cho mỗi lần Add.
        var uniqueSalesDays = new HashSet<DateOnly>();

        // Chuyển startDate và endDate sang DateOnly một lần duy nhất ở ngoài vòng lặp để tránh tính toán thừa
        DateOnly? filterStartDate = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value.UtcDateTime) : null;
        DateOnly? filterEndDate = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value.UtcDateTime) : null;

        // Tránh dùng LINQ để không tạo rác (Zero Allocation)
        foreach (var snapshot in snapshots)
        {
            foreach (var kvp in snapshot.DailyMetrics)
            {
                DateOnly currentDate = kvp.Key;
                DailyMetric metric = kvp.Value;

                // 1. Áp dụng Date-range filtering (Nếu có)
                if (filterStartDate.HasValue && currentDate < filterStartDate.Value)
                    continue;

                if (filterEndDate.HasValue && currentDate > filterEndDate.Value)
                    continue;

                // 2. Chỉ tính những ngày THỰC SỰ có phát sinh giao dịch bán hàng
                if (metric.QuantitySold > 0)
                {
                    totalQuantitySold += metric.QuantitySold;
                    uniqueSalesDays.Add(currentDate);
                }
            }
        }

        int numberOfSalesDays = uniqueSalesDays.Count;

        // XỬ LÝ EDGE CASE: Chia cho 0
        // Nếu không có ngày nào phát sinh doanh số (hoặc kho trống), trả về 0 ngay lập tức
        // Tuyệt đối không để hệ thống văng Exception DivideByZeroException
        if (numberOfSalesDays == 0)
        {
            return 0d; 
        }

        // Ép kiểu double để phép chia trả về số thập phân chính xác
        double averageDailySales = (double)totalQuantitySold / numberOfSalesDays;

        // Trả về kết quả đã được làm tròn 2 chữ số thập phân cho gọn gàng
        return Math.Round(averageDailySales, 2);
    }
}