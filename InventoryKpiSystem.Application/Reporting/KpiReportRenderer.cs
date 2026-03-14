

namespace InventoryKpiSystem.Application.Reporting;

// DTO siêu nhẹ để vận chuyển dữ liệu báo cáo
public record KpiResultDto(string KpiName, object? Value, string FormattedValue);

public interface IKpiReportRenderer
{
    Task RenderAndExportAsync(IEnumerable<KpiResultDto> results);
}

public class KpiReportRenderer : IKpiReportRenderer
{
    // Chứa danh sách tất cả các kênh xuất báo cáo
    private readonly IEnumerable<IKpiExporter> _exporters;

    public KpiReportRenderer(IEnumerable<IKpiExporter> exporters)
    {
        _exporters = exporters;
    }

    public async Task RenderAndExportAsync(IEnumerable<KpiResultDto> results)
    {
        // Chốt data 1 lần (tránh multiple enumeration)
        var resultList = results.ToList();

        // Kích hoạt tất cả các kênh xuất chạy ĐỒNG THỜI (Console và JSON chạy song song)
        var exportTasks = _exporters.Select(exporter => exporter.ExportAsync(resultList));

        await Task.WhenAll(exportTasks);
    }
}