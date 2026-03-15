using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InventoryKpiSystem.Application.Reporting;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryKpiSystem.Infrastructure.BackgroundTasks;

public class FileProcessingBackgroundService : BackgroundService
{
    private readonly IFileQueueConsumer _fileQueue;
    private readonly IAsyncFileParser<object> _fileParser; // Dùng Parser đa năng để đọc được cả 2 loại luồng
    private readonly IInventoryStateStore _stateStore;
    private readonly IIdempotencyRegistry _idempotencyRegistry;
    private readonly IEnumerable<IKpiCalculator> _kpiCalculators; 
    private readonly IKpiReportRenderer _reportRenderer;
    private readonly ILogger<FileProcessingBackgroundService> _logger;

    public FileProcessingBackgroundService(
        IFileQueueConsumer fileQueue,
        IAsyncFileParser<object> fileParser, 
        IInventoryStateStore stateStore,
        IIdempotencyRegistry idempotencyRegistry,
        IEnumerable<IKpiCalculator> kpiCalculators,
        IKpiReportRenderer reportRenderer,
        ILogger<FileProcessingBackgroundService> logger)
    {
        _fileQueue = fileQueue;
        _fileParser = fileParser;
        _stateStore = stateStore;
        _idempotencyRegistry = idempotencyRegistry;
        _kpiCalculators = kpiCalculators;
        _reportRenderer = reportRenderer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Kpi Orchestrator Background Service đang khởi chạy...");
        
        // Nhường luồng cho các service khác khởi động xong rồi mới bắt đầu nuốt file
        await Task.Yield();

        try
        {
            // Lắng nghe Channel liên tục (sẽ tự động Sleep nếu Channel trống, không tốn 1% CPU nào)
            await foreach (var filePath in _fileQueue.ReadAllFilesAsync(stoppingToken))
            {
                try
                {
                    string fileHash = Path.GetFileName(filePath);

                    // 🛡️ BẢN VÁ ĐA LUỒNG: Gọi hàm TryAdd Atomic (Nguyên tử). 
                    // Nếu trả về false nghĩa là file này đã có người xử lý rồi, văng ra ngoài ngay.
                    if (!_idempotencyRegistry.TryAdd(fileHash))
                    {
                        _logger.LogInformation("⏭️ Bỏ qua file đã xử lý hoặc đang xử lý: {FilePath}", filePath);
                        continue; 
                    }

                    _logger.LogInformation("⏳ Đang xử lý dữ liệu từ file: {FilePath}", filePath);

                    // 1. Kích hoạt luồng nạp dữ liệu Streaming (Zero Allocation)
                    await ProcessDataStreamingAsync(filePath, stoppingToken);

                    // 2. Kích hoạt luồng tính toán KPI và in báo cáo (Chạy sau khi đã nạp xong data)
                    await CalculateAndRenderKpisAsync();
                }
                catch (Exception ex)
                {
                    // Lỗi 1 file thì chỉ log lại và bỏ qua, tuyệt đối không làm chết Background Service
                    _logger.LogError(ex, "❌ Lỗi khi xử lý file {FilePath}", filePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 Background Service đang dừng an toàn (Graceful Shutdown)...");
        }
        catch (Exception ex) 
        {
            _logger.LogCritical(ex, "💥 Lỗi nghiêm trọng: Background Service bị crash hoàn toàn.");
        }
    }

    // =================================================================================
    // CÁC HÀM XỬ LÝ NGHIỆP VỤ BÊN DƯỚI (INTERNAL LOGIC)
    // =================================================================================

    private async Task ProcessDataStreamingAsync(string filePath, CancellationToken ct)
    {
        // Sử dụng IAsyncEnumerable để nạp từng dòng, file JSON có 10GB cũng không bao giờ tràn RAM
        await foreach (var record in _fileParser.ParseAsync(filePath, ct))
        {
            // Pattern Matching siêu mượt của C# 8+
            if (record is PurchaseOrder po)
            {
                var snapshot = _stateStore.GetOrAddSnapshot(po.ProductId);
                snapshot.UpdateWithOrder(po); // Đã có lock bên trong Entity bảo vệ
            }
            else if (record is SalesInvoice invoice)
            {
                var snapshot = _stateStore.GetOrAddSnapshot(invoice.ProductId);
                snapshot.UpdateWithInvoice(invoice); // Đã có lock bên trong Entity bảo vệ
            }
        }
    }

    private async Task CalculateAndRenderKpisAsync()
    {
        var allSnapshots = _stateStore.GetAllSnapshots();
        
        // Kho trống thì không cần in báo cáo rác ra màn hình
        if (allSnapshots.Count == 0) return;

        var results = new List<KpiResultDto>();

        // Duyệt qua toàn bộ các công thức KPI đã đăng ký qua Dependency Injection
        foreach (var calculator in _kpiCalculators)
        {
            try
            {
                var rawValue = calculator.Calculate(allSnapshots);
                
                // Pattern matching format dữ liệu siêu gọn
                string formattedValue = rawValue switch
                {
                    decimal d => d.ToString("C0"),
                    double db => db.ToString("N2"),
                    int i => i.ToString("N0"),
                    long l => l.ToString("N0"),
                    string s => s,
                    _ => rawValue?.ToString() ?? "0"
                };

                results.Add(new KpiResultDto(calculator.Name, rawValue, formattedValue));
            }
            catch (Exception ex)
            {
                // Nếu 1 công thức tính toán bị lỗi (VD chia cho 0), ghi nhận lỗi và tính tiếp công thức khác
                _logger.LogWarning(" 👉 Lỗi tính KPI {Name}: {Message}", calculator.Name, ex.Message);
                results.Add(new KpiResultDto(calculator.Name, "ERROR", "N/A"));
            }
        }
        
        // Giao việc in ấn cho Nhạc trưởng Reporting (sẽ kích hoạt cả luồng Console và Ghi File JSON)
        await _reportRenderer.RenderAndExportAsync(results);
    }
}