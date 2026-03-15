using InventoryKpiSystem.Core.Entities;

namespace InventoryKpiSystem.Core.Interfaces;
/// <summary>
/// Hợp đồng cho Service theo dõi thư mục (Real-time Monitoring).
/// </summary>
public interface IFileMonitorService
{
    void StartMonitoring();
    void StopMonitoring();
}