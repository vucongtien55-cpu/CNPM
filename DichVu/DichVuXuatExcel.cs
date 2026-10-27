using ClosedXML.Excel;

namespace MixueDatMon.DichVu;

/// <summary>
/// Hàm tiện ích xuất Excel dùng chung (đối soát cuối ngày, báo cáo lãi gộp, danh sách kho, ...).
/// Đăng ký Singleton vì không giữ trạng thái.
/// </summary>
public class DichVuXuatExcel
{
    public const string LoaiTep = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Tạo 1 sheet Excel đơn giản: vài dòng thông tin đầu trang, 1 dòng tiêu đề cột in đậm, rồi dữ liệu.
    /// </summary>
    /// <param name="tenSheet">Tên sheet (tối đa 31 ký tự theo giới hạn Excel, tự cắt nếu dài hơn).</param>
    /// <param name="tieuDe">Tiêu đề lớn ở dòng 1.</param>
    /// <param name="thongTinDau">Các dòng thông tin phụ (vd: "Ngày: 20/09/2026"), hiển thị ngay dưới tiêu đề.</param>
    /// <param name="cot">Tên các cột.</param>
    /// <param name="dong">Dữ liệu từng dòng, mỗi dòng là mảng object cùng số phần tử với <paramref name="cot"/>.</param>
    /// <param name="cotTien">Chỉ số (0-based) các cột cần định dạng số tiền (#,##0 đ).</param>
    public byte[] Tao(string tenSheet, string tieuDe, IEnumerable<string>? thongTinDau,
        string[] cot, IEnumerable<object?[]> dong, int[]? cotTien = null)
    {
        using var workbook = new XLWorkbook();
        var ten = tenSheet.Length > 31 ? tenSheet[..31] : tenSheet;
        var sheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(ten) ? "Sheet1" : ten);

        var dongHienTai = 1;
        sheet.Cell(dongHienTai, 1).Value = tieuDe;
        sheet.Cell(dongHienTai, 1).Style.Font.SetBold().Font.FontSize = 14;
        dongHienTai++;

        if (thongTinDau != null)
            foreach (var dong1 in thongTinDau)
            {
                sheet.Cell(dongHienTai, 1).Value = dong1;
                dongHienTai++;
            }
        dongHienTai++; // dòng trống ngăn cách

        var dongTieuDeCot = dongHienTai;
        for (var i = 0; i < cot.Length; i++)
        {
            var o = sheet.Cell(dongTieuDeCot, i + 1);
            o.Value = cot[i];
            o.Style.Font.SetBold();
            o.Style.Fill.BackgroundColor = XLColor.FromArgb(0xFF, 0xE3, 0xE3);
            o.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
        dongHienTai++;

        var cotTienSet = (cotTien ?? Array.Empty<int>()).ToHashSet();
        foreach (var hang in dong)
        {
            for (var i = 0; i < hang.Length; i++)
            {
                var o = sheet.Cell(dongHienTai, i + 1);
                var giaTri = hang[i];
                switch (giaTri)
                {
                    case null: o.Value = ""; break;
                    case DateTime dt: o.Value = dt; o.Style.DateFormat.Format = "dd/MM/yyyy HH:mm"; break;
                    case decimal or double or float or int or long:
                        o.Value = Convert.ToDouble(giaTri);
                        if (cotTienSet.Contains(i)) o.Style.NumberFormat.Format = "#,##0 \"đ\"";
                        break;
                    default: o.Value = giaTri.ToString(); break;
                }
            }
            dongHienTai++;
        }

        sheet.Columns(1, cot.Length).AdjustToContents();
        sheet.SheetView.FreezeRows(dongTieuDeCot);

        using var luong = new MemoryStream();
        workbook.SaveAs(luong);
        return luong.ToArray();
    }
}
