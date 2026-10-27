using System.Globalization;

namespace MixueDatMon.TroGiup;

public static class DinhDang
{
    /// <summary>25000 -> "25.000đ"</summary>
    public static string Tien(this decimal giaTri) =>
        ((long)giaTri).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + "đ";

    /// <summary>25000 -> "25K" (giống bảng giá MIXUE); giá lẻ thì hiển thị đầy đủ.</summary>
    public static string TienRutGon(this decimal giaTri) =>
        giaTri % 1000 == 0 ? $"{(long)(giaTri / 1000)}K" : giaTri.Tien();

    public static string SoThapPhan(this decimal giaTri) => giaTri.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Lớp CSS của nhãn trạng thái đơn.</summary>
    public static string LopTrangThai(int maTrangThai) => maTrangThai switch
    {
        1 => "warn", 3 => "ok", 4 => "ok", 5 => "red", _ => ""
    };
}
