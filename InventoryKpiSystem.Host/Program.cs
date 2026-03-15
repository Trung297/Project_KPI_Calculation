using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;
using InventoryKpiSystem.Infrastructure.BackgroundTasks;
using InventoryKpiSystem.Infrastructure.Channels;
using InventoryKpiSystem.Infrastructure.Data;
using InventoryKpiSystem.Infrastructure.FileMonitoring;
using InventoryKpiSystem.Infrastructure.FileProcessing;
using InventoryKpiSystem.Application.Kpi;
using InventoryKpiSystem.Application.Reporting; 

// 1. Khởi tạo Generic Host builder (Chuẩn Worker Service của .NET 8)
var builder = Host.CreateDefaultBuilder(args);

// 2. Cấu hình đọc file appsettings.json
builder.ConfigureAppConfiguration((hostingContext, config) =>
{
    config.SetBasePath(Directory.GetCurrentDirectory());
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
});

// 3. ĐĂNG KÝ DEPENDENCY INJECTION (DI CONTAINER)
builder.ConfigureServices((hostContext, services) =>
{
    // A. Đăng ký Cấu hình (Options Pattern)
    services.Configure<FileQueueSettings>(hostContext.Configuration.GetSection("FileQueueSettings"));
    services.Configure<FileMonitorSettings>(hostContext.Configuration.GetSection("FileMonitorSettings"));
    services.Configure<ReportingSettings>(hostContext.Configuration.GetSection("ReportingSettings"));

    // B. Đăng ký Kho lưu trữ State & Parsers
    // BẮT BUỘC LÀ SINGLETON để toàn bộ hệ thống xài chung 1 vùng RAM chứa danh sách sản phẩm
    services.AddSingleton<IInventoryStateStore, InMemoryInventoryStore>();
    
    // Đăng ký Xero Adapter đa năng (đọc streaming trả ra object: chứa cả Order và Invoice)
    services.AddSingleton<IAsyncFileParser<object>, XeroInvoiceStreamingAdapter>();

    // C. Đăng ký Hàng đợi Channel (Forwarding Interfaces - Kỹ thuật DI cấp cao)
    // Đảm bảo Producer và Consumer cùng trỏ về một bản thể Channel duy nhất
    services.AddSingleton<FileProcessingChannel>();
    services.AddSingleton<IFileQueueProducer>(sp => sp.GetRequiredService<FileProcessingChannel>());
    services.AddSingleton<IFileQueueConsumer>(sp => sp.GetRequiredService<FileProcessingChannel>());

    // D. Đăng ký Cơ chế chống trùng lặp (Idempotency) - Bọc thép đa luồng
    services.AddSingleton<IIdempotencyRegistry, InMemoryIdempotencyRegistry>();

    // E. Đăng ký Động cơ KPI (Strategy Pattern)
    // Hệ thống sẽ tự động gom tất cả các class khai báo bằng IKpiCalculator thành 1 danh sách (IEnumerable<IKpiCalculator>)
    services.AddSingleton<IKpiCalculator, TotalSkusCalculator>();
    services.AddSingleton<IKpiCalculator, OutOfStockCalculator>();
    services.AddSingleton<IKpiCalculator, StockValueCalculator>();
    services.AddSingleton<IKpiCalculator, AvgDailySalesCalculator>();
    services.AddSingleton<IKpiCalculator, AvgInventoryAgeCalculator>();

    // F. Đăng ký Bộ xuất báo cáo KPI (Strategy Pattern cho Reporting)
    // 1. Đăng ký các kênh xuất (Exporters chạy song song)
    services.AddSingleton<IKpiExporter, ConsoleKpiExporter>();
    services.AddSingleton<IKpiExporter, JsonFileKpiExporter>();
    // 2. Đăng ký Nhạc trưởng điều phối báo cáo (Sẽ tự động gom danh sách Exporters ở trên)
    services.AddSingleton<IKpiReportRenderer, KpiReportRenderer>();

    // G. Đăng ký dịch vụ theo dõi thư mục (Watcher)
    services.AddSingleton<IFileMonitorService, InventoryFileSystemWatcher>();

    // H. Đăng ký "Nhạc trưởng" chạy ngầm (Hosted Service)
    // Background Service sẽ tự động được .NET kích hoạt khi app.RunAsync()
    services.AddHostedService<FileProcessingBackgroundService>();
});

// 4. Build hệ thống
var app = builder.Build();

// 5. Khởi động FileSystemWatcher ngay trước khi ứng dụng chính thức chạy
// Cần lấy instance từ Services ra để kích hoạt "Đôi mắt" theo dõi thư mục
var fileMonitor = app.Services.GetRequiredService<IFileMonitorService>();
fileMonitor.StartMonitoring();

// 6. Chạy ứng dụng (Sẽ block ở đây và lắng nghe sự kiện thư mục / channel liên tục)
await app.RunAsync();