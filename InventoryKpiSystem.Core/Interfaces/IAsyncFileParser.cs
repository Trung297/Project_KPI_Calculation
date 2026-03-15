using InventoryKpiSystem.Core.Entities;


namespace InventoryKpiSystem.Core.Interfaces;

/// <summary>
/// Hợp đồng cho việc đọc và parse file JSON dung lượng lớn theo luồng (Streaming).
/// </summary>
public interface IAsyncFileParser<T>
{
    // Sử dụng IAsyncEnumerable để nạp dữ liệu từng dòng, tối ưu RAM tuyệt đối
    IAsyncEnumerable<T> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
