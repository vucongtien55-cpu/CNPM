using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MixueDatMon.DichVu;
using MixueDatMon.DuLieu;
using MixueDatMon.HubThoiGianThuc;

var xayDung = WebApplication.CreateBuilder(args);

xayDung.Services.AddControllersWithViews();
xayDung.Services.AddSignalR();                       // thời gian thực
xayDung.Services.AddDbContext<DuLieuContext>(tuyChon =>
    tuyChon.UseSqlServer(xayDung.Configuration.GetConnectionString("MacDinh")));
xayDung.Services.AddScoped<DichVuKho>();
xayDung.Services.AddScoped<DichVuDatHang>();
xayDung.Services.AddSingleton<DichVuVietQr>();
xayDung.Services.AddScoped<DichVuCaiDat>();
xayDung.Services.AddScoped<DichVuNhatKy>();
xayDung.Services.AddScoped<DichVuHuyDon>();
xayDung.Services.AddScoped<DichVuKhuyenMai>();
xayDung.Services.AddScoped<DichVuGiaVon>();
xayDung.Services.AddSingleton<DichVuXuatExcel>();
xayDung.Services.AddSingleton<DichVuGioiHanTanSuat>();
xayDung.Services.AddHostedService<DichVuTuHuyDon>();      // tự hủy đơn mang đi quá hạn thanh toán

// Session: nhớ bàn khách đã quét QR
xayDung.Services.AddSession(tuyChon =>
{
    tuyChon.IdleTimeout = TimeSpan.FromHours(4);
    tuyChon.Cookie.HttpOnly = true;
    tuyChon.Cookie.IsEssential = true;
});

// Đăng nhập bằng Cookie cho thu ngân / quản lý
xayDung.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(tuyChon =>
    {
        tuyChon.LoginPath = "/TaiKhoan/DangNhap";
        tuyChon.AccessDeniedPath = "/TaiKhoan/TuChoiTruyCap";
        tuyChon.ReturnUrlParameter = "duongDanQuayLai";
        tuyChon.ExpireTimeSpan = TimeSpan.FromHours(10);
        tuyChon.SlidingExpiration = true;
        tuyChon.Cookie.HttpOnly = true;
        tuyChon.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // đặt Always khi triển khai HTTPS
        tuyChon.Cookie.SameSite = SameSiteMode.Lax;
    });
xayDung.Services.AddAuthorization();

var ungDung = xayDung.Build();

if (!ungDung.Environment.IsDevelopment())
{
    ungDung.UseExceptionHandler("/TrangChu/Loi");
    ungDung.UseHsts();
}

// Đứng sau devtunnels/proxy vẫn lấy đúng IP thật của khách (dùng cho DichVuGioiHanTanSuat)
ungDung.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

ungDung.UseHttpsRedirection();
ungDung.UseStaticFiles();

// Dùng culture Invariant để số thập phân ("1.5") luôn được đọc đúng bất kể ngôn ngữ máy chủ
ungDung.UseRequestLocalization(tuyChon =>
{
    var danhSachCulture = new List<CultureInfo> { CultureInfo.InvariantCulture };
    tuyChon.DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture);
    tuyChon.SupportedCultures = danhSachCulture;
    tuyChon.SupportedUICultures = danhSachCulture;
    tuyChon.RequestCultureProviders.Clear();
});

ungDung.UseRouting();
ungDung.UseSession();
ungDung.UseAuthentication();
ungDung.UseAuthorization();

ungDung.MapHub<DonHangHub>("/hubs/donhang");

// Mã QR trỏ tới /t/{maQr}
ungDung.MapControllerRoute("ban", "t/{maQr}", new { controller = "ThucDon", action = "Ban" });
ungDung.MapControllerRoute("macdinh", "{controller=TrangChu}/{action=Index}/{id?}");

ungDung.Run();
