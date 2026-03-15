using System.Collections.Concurrent;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Infrastructure.Data;

/// <summary>
/// Triển khai bộ nhớ đệm siêu nhẹ để chặn file trùng lặp (Thread-safe).
/// </summary>
public class InMemoryIdempotencyRegistry : IIdempotencyRegistry
{
    // 🛡️ Bọc thép bằng ConcurrentDictionary để đảm bảo an toàn đa luồng tuyệt đối.
    // Key: Tên file (hoặc mã Hash) 
    // Value: 0 (Kiểu byte siêu nhẹ, tốn đúng 1 byte RAM, chỉ dùng làm "bù nhìn" để thỏa mãn cấu trúc Dictionary)
    private readonly ConcurrentDictionary<string, byte> _processedFiles = new();

    public bool TryAdd(string fileIdentifier)
    {
        // TUYỆT CHIÊU ATOMIC (Nguyên tử): 
        // Vừa kiểm tra sự tồn tại, vừa ghi nhận file mới trong đúng 1 nhịp CPU (Độ phức tạp O(1)).
        // - Trả về true: Nếu file chưa từng xuất hiện (Được phép đi tiếp vào luồng Parse JSON).
        // - Trả về false: Nếu file đã nằm trong danh sách (Bị chặn đứng ngay lập tức để chống tính đúp KPI).
        return _processedFiles.TryAdd(fileIdentifier, 0);
    }
}