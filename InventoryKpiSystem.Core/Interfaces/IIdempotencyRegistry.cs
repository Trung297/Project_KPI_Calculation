namespace InventoryKpiSystem.Core.Interfaces;

/// <summary>
/// Hợp đồng cho bộ phận kiểm tra file trùng lặp (Idempotency & Fault Tolerance).
/// </summary>
public interface IIdempotencyRegistry
{
    // TUYỆT CHIÊU ATOMIC: Vừa kiểm tra vừa đánh dấu trong đúng 1 nhịp.
    // Trả về true nếu file là MỚI và được phép xử lý.
    // Trả về false nếu file đã tồn tại và bị chặn lại.
    bool TryAdd(string fileIdentifier);
}