using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using QuanlyKTX.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace QuanlyKTX.Controllers
{
    public class AccountController : Controller
    {
        private readonly KtxthongminhContext _context;
        private static readonly Dictionary<string, (int Count, DateTime LockUntil)> _loginAttempts = new();
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        public AccountController(KtxthongminhContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete(".AspNetCore.Cookies");
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            return View();
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (_loginAttempts.TryGetValue(ip, out var attempt) && attempt.LockUntil > DateTime.Now)
            {
                int mins = (int)(attempt.LockUntil - DateTime.Now).TotalMinutes + 1;
                ViewBag.Error = $"Quá nhiều lần đăng nhập sai. Thử lại sau {mins} phút.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.";
                return View();
            }

            var user = await _context.TaiKhoans.FirstOrDefaultAsync(u => u.TenDangNhap == username);
            if (user == null)
            {
                ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu!";
                RecordFailedAttempt(ip);
                return View();
            }

            string hashedInput = (user.VaiTroId == 3) ? ComputeMd5Hash(password) : ComputeSha256Hash(password);

            if (user.MatKhauHash != hashedInput)
            {
                ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu!";
                RecordFailedAttempt(ip);
                return View();
            }

            if (user.TrangThai == false)
            {
                ViewBag.Error = "Tài khoản của bạn đã bị khóa.";
                return View();
            }

            _loginAttempts.Remove(ip);
            user.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            var roleName = user.VaiTroId switch { 1 => "Admin", 2 => "BaoVe", 4 => "GiaoVien", _ => "SinhVien" };
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.TenDangNhap),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("VaiTroId", user.VaiTroId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false });

            return RedirectBasedOnRole(user.VaiTroId);
        }

        // ==========================================================
        // KHẮC PHỤC LỖI 404 TRONG ẢNH: ACTION ACCESSDENIED
        // ==========================================================
        [HttpGet]
        public IActionResult AccessDenied()
        {
            // Nếu đã đăng nhập mà vào nhầm chỗ, đá về trang chủ tương ứng
            if (User.Identity.IsAuthenticated)
            {
                return RedirectBasedOnRole();
            }
            return RedirectToAction("Login");
        }

        [HttpGet]
        [Authorize(Roles = "SinhVien")]
        public IActionResult DoiMatKhau()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "SinhVien")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoiMatKhau(string currentPassword, string newPassword, string confirmPassword)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu mới và xác nhận không khớp.";
                return View();
            }

            var user = await _context.TaiKhoans.FirstOrDefaultAsync(u => u.TenDangNhap == username);
            if (user == null) return RedirectToAction("Logout");

            if (user.MatKhauHash != ComputeMd5Hash(currentPassword))
            {
                ViewBag.Error = "Mật khẩu hiện tại không chính xác.";
                return View();
            }

            user.MatKhauHash = ComputeMd5Hash(newPassword);
            _context.TaiKhoans.Update(user);

            var thongBao = new PhanAnh
            {
                Mssv = username,
                TieuDe = "Thay đổi bảo mật tài khoản",
                NoiDung = $"Sinh viên {username} đã đổi mật khẩu thành công vào {DateTime.Now:HH:mm:ss}.",
                LoaiSuCo = "Bảo mật",
                NgayGui = DateTime.Now,
                TrangThai = "Chờ tiếp nhận"
            };
            _context.PhanAnhs.Add(thongBao);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.";
            return RedirectToAction("Logout");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        private string ComputeMd5Hash(string rawData)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(rawData);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private void RecordFailedAttempt(string ip)
        {
            if (_loginAttempts.TryGetValue(ip, out var existing))
            {
                int newCount = existing.Count + 1;
                _loginAttempts[ip] = newCount >= MaxFailedAttempts ? (newCount, DateTime.Now.AddMinutes(LockoutMinutes)) : (newCount, DateTime.MinValue);
            }
            else { _loginAttempts[ip] = (1, DateTime.MinValue); }
        }

        private IActionResult RedirectBasedOnRole(int? vaiTroId = null)
        {
            if (vaiTroId == null)
            {
                var claim = User.FindFirst("VaiTroId")?.Value;
                vaiTroId = int.TryParse(claim, out int v) ? v : 0;
            }
            // Logic điều hướng về Admin Area hoặc trang chủ thường
            return (vaiTroId == 1 || vaiTroId == 2 || vaiTroId == 4)
                ? RedirectToAction("Index", "Home", new { area = "Admin" })
                : RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}