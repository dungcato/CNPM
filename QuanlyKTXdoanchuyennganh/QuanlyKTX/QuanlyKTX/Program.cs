using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Hubs;
using QuanlyKTX.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<KtxthongminhContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/TaiKhoan/Login";
        options.AccessDeniedPath = "/Admin/TaiKhoan/Login";
    });

builder.Services.AddAuthorization();


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

app.Use(async (context, next) =>
{
  
    if (context.User.Identity == null || !context.User.Identity.IsAuthenticated)
    {
        var claims = new[] {
            new Claim(ClaimTypes.Name, "Admin (Test Mode)"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Quản lý"),
            new Claim(ClaimTypes.Role, "Bảo vệ")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        context.User = new ClaimsPrincipal(identity);
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();


app.MapHub<CameraHub>("/cameraHub");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();