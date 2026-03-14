using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices; // 👉 Bổ sung thư viện này để quản lý Token
using System.Threading;
using System.Threading.Tasks;
using InventoryKpiSystem.Application.Reporting;
using InventoryKpiSystem.Core.Interfaces;
using InventoryKpiSystem.Infrastructure.BackgroundTasks;
using InventoryKpiSystem.Core.Entities; 
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InventoryKpiSystem.Tests.BackgroundTasks;

public class FileProcessingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ParserThrowsException_LogsErrorAndContinues()
    {
        // Arrange
        var mockQueue = new Mock<IFileQueueConsumer>();
        mockQueue.Setup(q => q.ReadAllFilesAsync(It.IsAny<CancellationToken>()))
                 .Returns(GetMockFilesAsync()); 

        var mockRegistry = new Mock<IIdempotencyRegistry>();
        mockRegistry.Setup(r => r.TryAdd(It.IsAny<string>())).Returns(true);

        var mockParser = new Mock<IAsyncFileParser<object>>();
        mockParser.Setup(p => p.ParseAsync("corrupt_file.txt", It.IsAny<CancellationToken>()))
                  .Throws(new Exception("Mocked Crash"));
        mockParser.Setup(p => p.ParseAsync("good_file.txt", It.IsAny<CancellationToken>()))
                  .Returns(GetEmptyAsyncEnumerable());

        var mockStore = new Mock<IInventoryStateStore>();
        var mockRenderer = new Mock<IKpiReportRenderer>();
        var mockLogger = new Mock<ILogger<FileProcessingBackgroundService>>();

        var service = new FileProcessingBackgroundService(
            mockQueue.Object, mockParser.Object, mockStore.Object, mockRegistry.Object, 
            new List<IKpiCalculator>(), mockRenderer.Object, mockLogger.Object);

        // Act
        using var cts = new CancellationTokenSource(); 
        await service.StartAsync(cts.Token);
        
        await Task.Delay(500); 
        await service.StopAsync(cts.Token); 

        // Assert
        mockParser.Verify(p => p.ParseAsync("corrupt_file.txt", It.IsAny<CancellationToken>()), Times.Once);
        mockParser.Verify(p => p.ParseAsync("good_file.txt", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GracefulShutdown_FinishesCurrentFileBeforeStopping()
    {
        // Arrange
        var mockQueue = new Mock<IFileQueueConsumer>();
        var tenFiles = new List<string> { "file_1.txt", "file_2.txt", "file_3.txt", "file_4.txt", "file_5.txt", "file_6.txt", "file_7.txt", "file_8.txt", "file_9.txt", "file_10.txt" };
        
        // 👉 CHUYỀN TOKEN XUỐNG CHO HÀM MOCK ĐỂ NÓ BIẾT ĐƯỜNG MÀ DỪNG
        mockQueue.Setup(q => q.ReadAllFilesAsync(It.IsAny<CancellationToken>()))
                 .Returns((CancellationToken ct) => GetSpecificMockFilesAsync(tenFiles, ct));

        var mockRegistry = new Mock<IIdempotencyRegistry>();
        mockRegistry.Setup(r => r.TryAdd(It.IsAny<string>())).Returns(true);

        var mockParser = new Mock<IAsyncFileParser<object>>();
        var mockStore = new Mock<IInventoryStateStore>();
        var mockRenderer = new Mock<IKpiReportRenderer>();
        var mockLogger = new Mock<ILogger<FileProcessingBackgroundService>>();
        var cts = new CancellationTokenSource();

        mockParser.Setup(p => p.ParseAsync("file_1.txt", It.IsAny<CancellationToken>()))
                  .Returns(SimulateProcessingAndCancelAsync(cts)); 

        var service = new FileProcessingBackgroundService(
            mockQueue.Object, mockParser.Object, mockStore.Object, mockRegistry.Object, 
            new List<IKpiCalculator>(), mockRenderer.Object, mockLogger.Object);

        // Act
        await service.StartAsync(cts.Token); 
        await Task.Delay(500);
        
        // Assert
        mockParser.Verify(p => p.ParseAsync("file_1.txt", It.IsAny<CancellationToken>()), Times.Once);
        mockParser.Verify(p => p.ParseAsync("file_2.txt", It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Helper Methods ---
    private async IAsyncEnumerable<string> GetMockFilesAsync()
    {
        yield return "corrupt_file.txt";
        yield return "good_file.txt";
        await Task.CompletedTask;
    }

    private async IAsyncEnumerable<object> GetEmptyAsyncEnumerable()
    {
        yield break;
    }

    // 👉 FIX: Yêu cầu hàng giả (Mock) phải tôn trọng tín hiệu Tắt cầu dao (Token)
    private async IAsyncEnumerable<string> GetSpecificMockFilesAsync(IEnumerable<string> files, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var file in files) 
        {
            ct.ThrowIfCancellationRequested(); // Nếu đã bị cắt điện thì văng lỗi, dừng nhả file!
            yield return file;
        }
    }

    private async IAsyncEnumerable<object> SimulateProcessingAndCancelAsync(CancellationTokenSource cts)
    {
        cts.Cancel(); 
        yield return new PurchaseOrder { ProductId = "MOCK-01", QuantityPurchased = 1 }; 
        await Task.CompletedTask;
    }
}