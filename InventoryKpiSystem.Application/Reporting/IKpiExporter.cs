using System.Collections.Generic;
using System.Threading.Tasks;

namespace InventoryKpiSystem.Application.Reporting;

public interface IKpiExporter
{
    Task ExportAsync(IReadOnlyList<KpiResultDto> results);
}