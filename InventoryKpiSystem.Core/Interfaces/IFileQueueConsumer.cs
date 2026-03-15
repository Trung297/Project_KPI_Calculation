using System.Collections.Generic;
using System.Threading;

// Dùng File-scoped namespace cho code gọn gàng, giảm 1 bậc lùi lề (indentation)
namespace InventoryKpiSystem.Core.Interfaces;

/// <summary>
/// Giao diện dành cho các Consumer (như Background Service) để lấy file từ hàng đợi ra xử lý.
/// </summary>
public interface IFileQueueConsumer
{
    /// <summary>
    /// Đọc liên tục các đường dẫn file từ hàng đợi bất đồng bộ.
    /// Hàm này sẽ "ngủ" (không tốn CPU) nếu hàng đợi đang trống.
    /// </summary>
    IAsyncEnumerable<string> ReadAllFilesAsync(CancellationToken cancellationToken = default);
}