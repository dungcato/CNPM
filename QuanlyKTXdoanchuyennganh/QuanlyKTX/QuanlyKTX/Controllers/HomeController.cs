using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanlyKTX.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace QuanlyKTX.Controllers
{
    [Authorize(Roles = "SinhVien")]
    public class HomeController : Controller
    {
        private readonly KtxthongminhContext _context;

        public HomeController(KtxthongminhContext context)
        {
            _context = context;
        }

        [AllowAnonymous] // Tạm mở cửa để chạy đoạn code bọc quyền bên dưới
        public async Task<IActionResult> Index()
        {
            // ============================================================
            // 0. AUTO-LOGIN CHẾ ĐỘ DEV: TỰ ĐỘNG ĐĂNG NHẬP MSSV 23574801
            // ============================================================
            if (!User.Identity.IsAuthenticated)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, "23574801"), // Bơm cứng MSSV này vào
                    new Claim(ClaimTypes.Role, "SinhVien"), // Tên Role
                    new Claim("VaiTroId", "3")              // ID Vai trò = 3 (Sinh Viên)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                // Cấp quyền xong thì load lại trang để nó ăn cái auth mới
                return RedirectToAction("Index");
            }

            // ============================================================
            // DỮ LIỆU TRANG CHỦ TỪ ĐÂY TRỞ XUỐNG GIỮ NGUYÊN
            // ============================================================
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(mssvHienTai)) return RedirectToAction("Login", "Account");

            var sv = _context.SinhViens.FirstOrDefault(s => s.Mssv == mssvHienTai);
            if (sv == null) return RedirectToAction("Logout", "Account");

            ViewBag.HoTenSV = sv.HoTen;
            ViewBag.MssvSV = sv.Mssv;

            var phong = _context.Phongs.FirstOrDefault(p => p.PhongId == sv.PhongDangOid);
            var toa = phong != null ? _context.ToaNhas.FirstOrDefault(t => t.ToaNhaId == phong.ToaNhaId) : null;

            // Lấy 3 thông báo mới nhất
            var thongBaos = _context.ThongBaos.OrderByDescending(t => t.NgayGui).Take(3).ToList();

            // Lấy 4 yêu cầu (phản ánh/chuyển phòng) gần nhất
            var dsYeuCau = _context.PhanAnhs.Where(p => p.Mssv == mssvHienTai).OrderByDescending(p => p.NgayGui).Take(4).ToList();

            // Lấy 3 nhật ký an ninh quan trọng nhất (như người lạ, xâm nhập)
            var anNinhs = _context.NhatKyAnNinhs.OrderByDescending(a => a.ThoiGian).Take(3).ToList();

            var model = new DashboardViewModel
            {
                SinhVien = sv,
                TenPhong = phong?.TenPhong ?? "N/A",
                TenToa = toa?.TenToa ?? "",
                TongTienHoaDon = _context.HoaDons.Where(h => h.Mssv == mssvHienTai && h.TrangThai == "Chưa thanh toán").Sum(h => (decimal?)h.TongTien) ?? 0,
                SoPhanAnhCho = _context.PhanAnhs.Count(p => p.Mssv == mssvHienTai && p.TrangThai == "Chờ tiếp nhận"),
                DanhSachThongBao = thongBaos,
                LichSuAnNinh = anNinhs,
                DanhSachYeuCauGanDay = dsYeuCau
            };

            return View(model);
        }
    }
}