using InventoryKpiSystem.Core.Entities;

namespace InventoryKpiSystem.Core.Interfaces;

/// <summary>
/// Hợp đồng nền tảng cho mọi thuật toán tính KPI (Strategy Pattern).
/// </summary>
public interface IKpiCalculator
{
    // Tên của KPI (ví dụ: "Total SKUs", "Out-of-Stock Items")
    string Name { get; }

    // Nâng cấp 1: Dùng IReadOnlyCollection để bảo vệ State không bị sửa đổi ngầm.
    // Nâng cấp 2: Thêm startDate và endDate (nullable) để dọn đường cho tính năng Bonus Date-range filtering.
    object Calculate(
        IReadOnlyCollection<ProductStateSnapshot> snapshots, 
        DateTimeOffset? startDate = null, 
        DateTimeOffset? endDate = null);
}
