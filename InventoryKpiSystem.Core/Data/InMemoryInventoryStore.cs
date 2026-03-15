using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using InventoryKpiSystem.Core.Entities;
using InventoryKpiSystem.Core.Interfaces;

namespace InventoryKpiSystem.Infrastructure.Data;

public class InMemoryInventoryStore : IInventoryStateStore
{
    // 🛡️ Dùng ConcurrentDictionary để đảm bảo Thread-Safe tuyệt đối trong môi trường Multi-thread
    private readonly ConcurrentDictionary<string, ProductStateSnapshot> _store = new();

    public ProductStateSnapshot GetOrAddSnapshot(string productId)
    {
        // TUYỆT CHIÊU ĐA LUỒNG (Atomic Operation): 
        // Hàm GetOrAdd sẽ khóa mảng (bucket lock) ở mức độ siêu nhỏ. 
        // Nó vừa tìm kiếm, nếu không có thì sẽ tự khởi tạo và Add vào một cách an toàn.
        // Đảm bảo không bao giờ có 2 luồng cùng lúc tạo ra 2 object Snapshot cho cùng 1 ProductId.
        return _store.GetOrAdd(productId, id => new ProductStateSnapshot { ProductId = id });
    }

    // Trả về IReadOnlyCollection để bảo vệ dữ liệu gốc không bị bên ngoài tự ý thêm/xóa
    public IReadOnlyCollection<ProductStateSnapshot> GetAllSnapshots() 
    {
        // Ép kiểu an toàn. ConcurrentDictionary.Values đã implement sẵn IReadOnlyCollection trong .NET
        return (IReadOnlyCollection<ProductStateSnapshot>)_store.Values;
    }
}