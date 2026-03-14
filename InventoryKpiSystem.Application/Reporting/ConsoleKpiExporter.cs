using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace InventoryKpiSystem.Application.Reporting;

public class ConsoleKpiExporter : IKpiExporter
{
    public Task ExportAsync(IReadOnlyList<KpiResultDto> results)
    {
        var sb = new StringBuilder();

        int nameWidth = 40;
        int valueWidth = 20;
        string line = new string('-', nameWidth + valueWidth + 7);

        sb.AppendLine("\n" + line);
        sb.AppendLine($"| {"KPI DESCRIPTION".PadRight(nameWidth)} | {"VALUE".PadLeft(valueWidth)} |");
        sb.AppendLine(line);

        foreach (var r in results)
        {
            sb.AppendLine($"| {r.KpiName.PadRight(nameWidth)} | {r.FormattedValue.PadLeft(valueWidth)} |");
        }
        sb.AppendLine(line + "\n");

        // In ra màn hình một lần duy nhất
        Console.WriteLine(sb.ToString());

        return Task.CompletedTask; // Console không có I/O bất đồng bộ nặng, nên trả về CompletedTask
    }
}