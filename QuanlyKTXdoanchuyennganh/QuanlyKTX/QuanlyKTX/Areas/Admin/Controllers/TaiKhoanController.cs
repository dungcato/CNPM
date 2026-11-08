using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class TaiKhoanController : Controller
    {
        private readonly KtxthongminhContext _context;

        public TaiKhoanController(KtxthongminhContext context)
        {
            _context = context;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var vaiTroClaim = User.FindFirst("VaiTroId")?.Value;
            if (vaiTroClaim != "1")
            {
                TempData["Error"] = "Cảnh báo bảo mật: Bạn không có quyền truy cập vào mục Quản lý Tài khoản!";
                context.Result = RedirectToAction("Index", "Home", new { area = "Admin" });
            }
            base.OnActionExecuting(context);
        }

        [HttpGet]
        public async Task<IActionResult> Index(string searchString, int? searchVaiTro, string searchTrangThai)
        {
            var query = _context.TaiKhoans.Include(t => t.VaiTro).AsQueryable();
            if (!string.IsNullOrEmpty(searchString)) query = query.Where(t => t.TenDangNhap.Contains(searchString));
            if (searchVaiTro.HasValue) query = query.Where(t => t.VaiTroId == searchVaiTro.Value);
            if (!string.IsNullOrEmpty(searchTrangThai))
            {
                bool isActive = searchTrangThai == "active";
                query = query.Where(t => t.TrangThai == isActive);
            }

            var danhSach = await query.OrderByDescending(t => t.NgayTao).ToListAsync();
            ViewBag.Total = await _context.TaiKhoans.CountAsync();
            ViewBag.Active = await _context.TaiKhoans.CountAsync(t => t.TrangThai == true);
            ViewBag.Banned = await _context.TaiKhoans.CountAsync(t => t.TrangThai == false);
            ViewBag.AdminCount = await _context.TaiKhoans.CountAsync(t => t.VaiTroId == 1);
            ViewBag.VaiTroList = await _context.VaiTros.ToListAsync();

            return View(danhSach);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapTaiKhoan(string TenDangNhap, int VaiTroId)
        {
            try
            {
                if (await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == TenDangNhap))
                {
                    TempData["Error"] = $"Tên đăng nhập '{TenDangNhap}' đã tồn tại!";
                    return RedirectToAction(nameof(Index));
                }

                // LOGIC 2 ĐOẠN MÃ HÓA CỦA MÀY ĐÂY:
                // Nếu là Sinh viên (ID=3) dùng MD5, còn lại dùng SHA256
                string hashPassword = (VaiTroId == 3)
                    ? ComputeMd5Hash("123456")
                    : ComputeSha256Hash("123456");

                var tkMoi = new TaiKhoan
                {
                    TenDangNhap = TenDangNhap,
                    MatKhauHash = hashPassword,
                    VaiTroId = VaiTroId,
                    TrangThai = true,
                    NgayTao = DateTime.Now
                };

                _context.TaiKhoans.Add(tkMoi);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cấp tài khoản '{TenDangNhap}'! Pass mặc định: 123456.";
            }
            catch (Exception ex) { TempData["Error"] = "Lỗi: " + ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var tk = await _context.TaiKhoans.FindAsync(id);
            if (tk != null)
            {
                // Reset cũng phải đúng kiểu mã hóa theo vai trò
                tk.MatKhauHash = (tk.VaiTroId == 3)
                    ? ComputeMd5Hash("123456")
                    : ComputeSha256Hash("123456");

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã reset mật khẩu {tk.TenDangNhap} về mặc định (123456).";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // 2 ĐOẠN MÃ HÓA CỦA MÀY ĐÂY
        // ==========================================

        // 1. Hàm băm MD5 cho Sinh viên
        private string ComputeMd5Hash(string rawData)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(rawData);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        // 2. Hàm băm SHA256 cho Admin/Staff
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
    }
}