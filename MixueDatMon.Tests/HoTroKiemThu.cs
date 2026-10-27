using Microsoft.EntityFrameworkCore;
using MixueDatMon.DuLieu;

namespace MixueDatMon.Tests;

/// <summary>Tạo 1 DuLieuContext chạy trên database trong bộ nhớ (EF Core InMemory) - mỗi bài test có database riêng, độc lập.</summary>
public static class HoTroKiemThu
{
    public static DuLieuContext TaoContext()
    {
        var tuyChon = new DbContextOptionsBuilder<DuLieuContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DuLieuContext(tuyChon);
    }
}
