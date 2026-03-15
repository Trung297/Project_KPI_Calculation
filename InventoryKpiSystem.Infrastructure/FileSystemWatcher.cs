using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using InventoryKpiSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryKpiSystem.Infrastructure.FileMonitoring;

// 1. Cấu hình đã tinh gọn, chỉ còn 1 thư mục Transaction
public class FileMonitorSettings
{
    public string InvoiceDirectory { get; set; } = string.Empty;
}

// 2. Dịch vụ theo dõi thư mục
public class InventoryFileSystemWatcher : IFileMonitorService, IDisposable
{
    private readonly FileMonitorSettings _settings;
    private readonly IFileQueueProducer _queueProducer; 
    private readonly ILogger<InventoryFileSystemWatcher> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();

    public InventoryFileSystemWatcher(
        IOptions<FileMonitorSettings> options,
        IFileQueueProducer queueProducer,
        ILogger<InventoryFileSystemWatcher> logger)
    {
        _settings = options.Value;
        _queueProducer = queueProducer;
        _logger = logger;
    }

    public void StartMonitoring()
    {
        // Chỉ setup 1 Watcher duy nhất cho thư mục transactions
        SetupWatcher(_settings.InvoiceDirectory, "Transactions");

        _logger.LogInformation("🚀 FileSystemWatcher đã khởi động và đang theo dõi thư mục Xero.");
    }

    public void StopMonitoring()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        _logger.LogInformation("🛑 Đã dừng toàn bộ FileSystemWatcher.");
    }

    private void SetupWatcher(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("Thư mục theo dõi bị trống trong file cấu hình!");
            return;
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogInformation("Đã tạo thư mục mới: {Path}", path);
        }

        // 🛑 QUAN TRỌNG: Đã đổi từ *.json sang *.txt để bắt file Xero
        var watcher = new FileSystemWatcher(path, "*.txt");

        watcher.Created += async (s, e) => await SafeHandleNewFileEvent(e.FullPath, label);
        watcher.Renamed += async (s, e) => await SafeHandleNewFileEvent(e.FullPath, label);

        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
        
        _logger.LogInformation("[*] Đang theo dõi {Label} (.txt) tại: {Path}", label, path);
    }

    private async Task SafeHandleNewFileEvent(string filePath, string label)
    {
        try
        {
            _logger.LogInformation("[DETECTED] Phát hiện file mới tại {Label}: {FileName}", label, Path.GetFileName(filePath));

            bool isReady = await WaitForFileAccess(filePath);

            if (isReady)
            {
                _logger.LogInformation("[READY] File đã sẵn sàng: {FileName}. Đang đẩy vào hàng đợi...", Path.GetFileName(filePath));
                
                // Đẩy file vào Channel
                await _queueProducer.EnqueueFileAsync(filePath);
            }
            else
            {
                _logger.LogWarning("[TIMEOUT] Bỏ qua file {FileName} do bị chiếm dụng quá lâu.", Path.GetFileName(filePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi bất ngờ khi xử lý sự kiện file {FileName}", Path.GetFileName(filePath));
        }
    }

    private async Task<bool> WaitForFileAccess(string filePath)
    {
        int maxRetries = 5;
        int delayMilliseconds = 1000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                _logger.LogDebug("[RETRY] File đang bị lock, thử lại lần {RetryCount}...", i + 1);
                await Task.Delay(delayMilliseconds);
            }
        }
        return false;
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}