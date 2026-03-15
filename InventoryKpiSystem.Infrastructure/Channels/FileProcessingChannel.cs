using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using InventoryKpiSystem.Core.Interfaces;
using Microsoft.Extensions.Options;

// Sử dụng File-scoped namespace cho code gọn gàng (Chuẩn C# 10+)
namespace InventoryKpiSystem.Infrastructure.Channels;

// Định nghĩa class cấu hình để map tự động với file appsettings.json
public class FileQueueSettings
{
    // Sức chứa tối đa của hàng đợi. 
    // Giúp kiểm soát RAM, tránh tình trạng hàng vạn file ập đến làm tràn bộ nhớ (Out of Memory).
    public int Capacity { get; set; } = 1000; 
}

/// <summary>
/// Trái tim điều phối luồng xử lý file ngầm.
/// Triển khai cả 2 interface Producer và Consumer nhưng sẽ được DI Container phân quyền chặt chẽ.
/// </summary>
public class FileProcessingChannel : IFileQueueProducer, IFileQueueConsumer
{
    private readonly Channel<string> _channel;

    // Tiêm IOptions để lấy cấu hình từ appsettings.json một cách Strongly-typed
    public FileProcessingChannel(IOptions<FileQueueSettings> options)
    {
        var capacity = options.Value.Capacity;

        var channelOptions = new BoundedChannelOptions(capacity)
        {
            // BACKPRESSURE: Nếu hàng đợi đầy (đạt 1000 file), người gửi (File Watcher) sẽ phải đứng chờ (Wait).
            // Điều này ép hệ điều hành/ổ cứng chậm lại, nhường CPU cho Background Service xử lý bớt file cũ.
            FullMode = BoundedChannelFullMode.Wait,
            
            // Có thể có nhiều sự kiện FileSystemWatcher cùng đẩy file vào
            SingleWriter = false, 
            
            // CHỈ CÓ 1 Background Service rút file ra xử lý tuần tự -> Tối ưu hóa lock ngầm của .NET
            SingleReader = true   
        };

        // Khởi tạo kênh giao tiếp bị giới hạn sức chứa (Bounded Channel)
        _channel = Channel.CreateBounded<string>(channelOptions);
    }

    public async ValueTask EnqueueFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // ValueTask giúp tiết kiệm chi phí cấp phát bộ nhớ (Heap allocation) 
        // khi Channel chưa bị đầy và có thể ghi ngay lập tức.
        await _channel.Writer.WriteAsync(filePath, cancellationToken);
    }

    public IAsyncEnumerable<string> ReadAllFilesAsync(CancellationToken cancellationToken = default)
    {
        // IAsyncEnumerable giúp Consumer (Background Service) rút data ra dạng Streaming.
        // Nếu Channel rỗng, nó sẽ tự động Sleep luồng (không ngốn CPU) cho đến khi có file mới.
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}