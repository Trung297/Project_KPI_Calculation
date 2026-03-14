using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InventoryKpiSystem.Application.Reporting; // Chứa JsonFileKpiExporter và KpiResultDto
using Microsoft.Extensions.Options;
using Xunit;

namespace InventoryKpiSystem.Tests.Infrastructure;

public class JsonFileKpiExporterTests
{
    private readonly string _testDir = "./test_reports_output";
    private readonly JsonFileKpiExporter _exporter;

    public JsonFileKpiExporterTests()
    {
        // Mock IOptions để bơm đường dẫn test vào
        var options = Options.Create(new ReportingSettings { ExportDirectory = _testDir });
        _exporter = new JsonFileKpiExporter(options);
    }

    [Fact]
    public async Task ExportAsync_DirectoryDeleted_AutoRecreatesDirectory()
    {
        // Arrange
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        var dummyData = new List<KpiResultDto>();

        // Act
        await _exporter.ExportAsync(dummyData);

        // Assert: Thư mục phải tự động sống lại
        Assert.True(Directory.Exists(_testDir));
        
        // Cleanup
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task ExportAsync_50ThreadsWriting_SemaphoreProtectsFileLock()
    {
        // Arrange
        var dummyData = new List<KpiResultDto> { new KpiResultDto("Test KPI", 100, "100") };

        // Act: Kéo 50 luồng xông vào ghi file CÙNG MỘT LÚC
        var tasks = Enumerable.Range(0, 50).Select(_ => _exporter.ExportAsync(dummyData)).ToList();
        
        // Assert: Không có exception (IOException: File is in use) nào được văng ra nhờ SemaphoreSlim
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(exception);

        // Cleanup
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
    }
}