using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InventoryKpiSystem.Core.Interfaces;
using InventoryKpiSystem.Infrastructure.FileMonitoring; 
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace InventoryKpiSystem.Tests.Infrastructure;

public class InventoryFileSystemWatcherTests
{
    private readonly string _testDir = "./test_watch_dir";

    [Fact]
    public async Task FileWatcher_WhenFileIsLocked_RetriesAndEventuallyQueuesFile()
    {
        // Arrange
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        Directory.CreateDirectory(_testDir);

        var mockQueue = new Mock<IFileQueueProducer>();
        var mockLogger = new Mock<ILogger<InventoryFileSystemWatcher>>();
        
        // 👉 Fix CS0117: Đổi tên WatchDirectory thành InvoiceDirectory theo như cấu hình của Tech Lead
        // (Nếu IDE vẫn báo đỏ, bạn trỏ chuột vào FileMonitorSettings xem Tech Lead đặt tên chính xác là gì nhé, ví dụ: DirectoryPath)
        var options = Options.Create(new FileMonitorSettings { InvoiceDirectory = _testDir });

        // 👉 Fix CS1503: Đảo lại thứ tự tham số truyền vào Constructor (Options đứng trước, Queue đứng sau)
        using var watcher = new InventoryFileSystemWatcher(options, mockQueue.Object, mockLogger.Object);
        watcher.StartMonitoring();

        string testFile = Path.Combine(_testDir, "locked_file.txt");

        // Act
        using (var lockedStream = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await lockedStream.WriteAsync(new byte[] { 1, 2, 3 }); 
            await lockedStream.FlushAsync();
            await Task.Delay(1500); 
        } 

        await Task.Delay(1000); 

        // Assert
        mockQueue.Verify(q => q.EnqueueFileAsync(It.Is<string>(path => path.Contains("locked_file.txt")), It.IsAny<CancellationToken>()), Times.Once);

        // Cleanup
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
    }
}