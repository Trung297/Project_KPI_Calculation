using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InventoryKpiSystem.Infrastructure.Data; // Đã trỏ đúng namespace theo source code
using Xunit;

namespace InventoryKpiSystem.Tests.Infrastructure;

public class IdempotencyRegistryTests
{
    [Fact]
    public async Task TryAdd_ConcurrentAccess_100Threads_OnlyOneSucceeds()
    {
        // Arrange
        var registry = new InMemoryIdempotencyRegistry();
        var fileName = "invoices_1.txt";
        int successCount = 0;

        // Act: Dùng Task.WhenAll kích hoạt 100 luồng bắn phá cùng lúc
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            await Task.Yield(); // Ép các thread đợi nhau để xuất phát đồng thời
            if (registry.TryAdd(fileName))
            {
                Interlocked.Increment(ref successCount);
            }
        });

        await Task.WhenAll(tasks);

        // Assert: Chống trùng lặp tuyệt đối, 99 luồng phải bị block, chỉ 1 luồng lọt qua
        Assert.Equal(1, successCount);
    }
}