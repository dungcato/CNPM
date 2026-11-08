using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuanlyKTX.Models;
using QuanlyKTX.Hubs; // Bắt buộc phải có cho Camera AI

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database
builder.Services.AddDbContext<KtxthongminhContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Cấu hình Cookie ĐĂNG NHẬP THẬT (Gắt hơn)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Chỉnh đường dẫn về đúng Controller đăng nhập của ông
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // Cấu hình Cookie bảo mật cao
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// 3. Cấu hình Phân quyền gắt
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Admin", "GiaoVien", "BaoVe"));
    options.AddPolicy("SinhVienOnly", policy => policy.RequireRole("SinhVien"));
});

// 4. KÍCH HOẠT SIGNALR (Cho luồng Camera Realtime)
builder.Services.AddSignalR();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 5. THỨ TỰ NÀY LÀ BẮT BUỘC KHÔNG ĐƯỢC SAI
app.UseAuthentication();
app.UseAuthorization();

// 6. GIỮ LẠI ĐƯỜNG DẪN CỦA CAMERA AI
app.MapHub<CameraHub>("/cameraHub");

// 7. Cấu hình điều hướng
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Chạy thẳng vào trang Đăng nhập làm mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();