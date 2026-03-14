using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace InventoryKpiSystem.Application.Reporting;
public class ReportingSettings
{
    public string ExportDirectory { get; set; } = string.Empty;
}
public class JsonFileKpiExporter : IKpiExporter
{
    private readonly string _exportDirectory;
    private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

    public JsonFileKpiExporter(IOptions<ReportingSettings> options)
    {
        // Lấy đường dẫn từ file cấu hình, không còn hard-code nữa!
        _exportDirectory = options.Value.ExportDirectory; 
    }

    public async Task ExportAsync(IReadOnlyList<KpiResultDto> results)
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!Directory.Exists(_exportDirectory))
            {
                Directory.CreateDirectory(_exportDirectory);
            }

            string hourlyFileName = $"kpi_report_{DateTimeOffset.UtcNow:yyyyMMdd_HH}.json";
            string hourlyFilePath = Path.Combine(_exportDirectory, hourlyFileName);
            string latestFilePath = Path.Combine(_exportDirectory, "latest_kpi_report.json");

            var options = new JsonSerializerOptions { WriteIndented = true };

            await SafeWriteFileAsync(hourlyFilePath, results, options);
            await SafeWriteFileAsync(latestFilePath, results, options);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SafeWriteFileAsync(string filePath, IReadOnlyList<KpiResultDto> results, JsonSerializerOptions options)
    {
        int maxRetries = 3;
        int delayMs = 500;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 4096, useAsync: true);
                
                // Đã fix lỗi cú pháp ở đây: Xóa toán tử 3 ngôi vô nghĩa
                await JsonSerializer.SerializeAsync(stream, results, options);
                return; 
            }
            catch (IOException)
            {
                if (i == maxRetries - 1)
                {
                    Console.WriteLine($"[CẢNH BÁO] Bỏ qua ghi file {Path.GetFileName(filePath)} lượt này do HĐH chiếm dụng quá lâu.");
                    return;
                }
                await Task.Delay(delayMs);
            }
        }
    }
}