using System.Threading;
using System.Threading.Tasks;

// Sử dụng File-scoped namespace của C# 10+/.NET 8
namespace InventoryKpiSystem.Core.Interfaces;

/// <summary>
/// Giao diện dành cho các Producer (như FileWatcher) để đẩy file mới vào hàng đợi xử lý.
/// </summary>
public interface IFileQueueProducer
{
    ValueTask EnqueueFileAsync(string filePath, CancellationToken cancellationToken = default);
}