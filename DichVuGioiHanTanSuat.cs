using System.Collections.Concurrent;

namespace MixueDatMon.DichVu;

/// <summary>
/// Giới hạn tần suất thao tác (chống spam) theo khóa bất kỳ (IP, số bàn, ...).
/// Dùng cửa sổ trượt (sliding window) lưu trong bộ nhớ, đủ dùng cho 1 tiến trình web.
/// Đăng ký là Singleton trong Program.cs.
/// </summary>
public class DichVuGioiHanTanSuat
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _lichSu = new();
    private readonly object _khoaDonGian = new();

    /// <summary>
    /// Kiểm tra khóa <paramref name="khoa"/> có được phép thực hiện thêm 1 lần nữa không,
    /// trong tối đa <paramref name="soLanToiDa"/> lần / <paramref name="khoangThoiGian"/>.
    /// Nếu được phép thì tự động ghi nhận lần này luôn (không cần gọi thêm hàm nào khác).
    /// </summary>
    public bool ChoPhep(string khoa, int soLanToiDa, TimeSpan khoangThoiGian, out TimeSpan thoiGianCho)
    {
        thoiGianCho = TimeSpan.Zero;
        var bayGio = DateTime.UtcNow;
        var hangDoi = _lichSu.GetOrAdd(khoa, _ => new Queue<DateTime>());

        lock (hangDoi)
        {
            // Bỏ các mốc đã quá cũ ra khỏi cửa sổ
            while (hangDoi.Count > 0 && bayGio - hangDoi.Peek() > khoangThoiGian)
                hangDoi.Dequeue();

            if (hangDoi.Count >= soLanToiDa)
            {
                var moiNhat = hangDoi.Peek();
                thoiGianCho = khoangThoiGian - (bayGio - moiNhat);
                if (thoiGianCho < TimeSpan.Zero) thoiGianCho = TimeSpan.Zero;
                return false;
            }

            hangDoi.Enqueue(bayGio);
            return true;
        }
    }

    /// <summary>Lấy địa chỉ IP thực của khách, có xét header X-Forwarded-For khi chạy sau proxy/devtunnel.</summary>
    public static string LayIp(HttpContext ngonNgu)
    {
        var forwarded = ngonNgu.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var phan = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (phan.Length > 0) return phan[0];
        }
        return ngonNgu.Connection.RemoteIpAddress?.ToString() ?? "khong-ro-ip";
    }

    /// <summary>Định dạng thời gian chờ thành câu tiếng Việt dễ đọc, ví dụ "32 giây" hoặc "2 phút".</summary>
    public static string DinhDangThoiGianCho(TimeSpan thoiGian)
    {
        if (thoiGian.TotalMinutes >= 1)
            return $"{Math.Ceiling(thoiGian.TotalMinutes)} phút";
        return $"{Math.Max(1, Math.Ceiling(thoiGian.TotalSeconds))} giây";
    }

    /// <summary>Dọn định kỳ các khóa không còn hoạt động để tránh phình bộ nhớ (gọi thủ công nếu cần, không bắt buộc).</summary>
    public void DonDep(TimeSpan quaHan)
    {
        var bayGio = DateTime.UtcNow;
        foreach (var kvp in _lichSu)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.Count == 0 || bayGio - kvp.Value.Last() > quaHan)
                    _lichSu.TryRemove(kvp.Key, out _);
            }
        }
    }
}
