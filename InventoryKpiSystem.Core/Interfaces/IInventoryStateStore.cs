using System.Collections.Generic;
using InventoryKpiSystem.Core.Entities;

namespace InventoryKpiSystem.Core.Interfaces;

/// <summary>
/// Hợp đồng quản lý trạng thái tồn kho tập trung của toàn hệ thống.
/// </summary>
public interface IInventoryStateStore
{
    // Lấy snapshot của một sản phẩm, nếu chưa có thì tạo mới và trả về
    ProductStateSnapshot GetOrAddSnapshot(string productId);
    
    // Lấy toàn bộ danh sách snapshot hiện có để phục vụ tính toán KPI
    IReadOnlyCollection<ProductStateSnapshot> GetAllSnapshots();
}