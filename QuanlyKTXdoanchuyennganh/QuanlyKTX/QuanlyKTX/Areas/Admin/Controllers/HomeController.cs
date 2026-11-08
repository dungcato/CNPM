using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,GiaoVien,BaoVe")] // ← VaiTroId 1, 2, 4 mới vào được
    public class HomeController : Controller
    {
        private readonly KtxthongminhContext _context;

        public HomeController(KtxthongminhContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. DỮ LIỆU CÁC THẺ THỐNG KÊ (TOP CARDS)
            ViewBag.TongSinhVien = await _context.SinhViens
                .CountAsync(s => s.TrangThai == "Đang lưu trú");

            ViewBag.SucChuaToiDa = await _context.Phongs
                .Include(p => p.LoaiPhong)
                .Where(p => p.TrangThai != "Đang bảo trì")
                .SumAsync(p => p.LoaiPhong != null ? p.LoaiPhong.SoNguoiToiDa : 0);

            ViewBag.PhanAnhCho = await _context.PhanAnhs
                .CountAsync(p => p.TrangThai == "Chờ tiếp nhận" || string.IsNullOrEmpty(p.TrangThai));

            ViewBag.PhongTrong = await _context.Phongs
                .CountAsync(p => p.TrangThai == "Còn trống");

            ViewBag.CanhBaoAnNinh = 4;

            // 2. DỮ LIỆU BIỂU ĐỒ (TÌNH TRẠNG LƯU TRÚ THEO TÒA)
            var thongKeToaNha = await _context.Phongs
                .Include(p => p.SinhViens)
                .Where(p => p.ToaNhaId != null)
                .GroupBy(p => p.ToaNhaId)
                .Select(g => new {
                    ToaNha = "Tòa " + g.Key,
                    SoLuongSV = g.SelectMany(p => p.SinhViens)
                                 .Count(s => s.TrangThai == "Đang lưu trú")
                })
                .ToListAsync();

            ViewBag.ChartLabels = thongKeToaNha.Select(t => t.ToaNha).ToList();
            ViewBag.ChartData = thongKeToaNha.Select(t => t.SoLuongSV).ToList();

            // 3. DANH SÁCH SINH VIÊN MỚI KÝ HỢP ĐỒNG
            var dsHopDongMoi = await _context.HopDongs
                .Include(h => h.MssvNavigation)
                .OrderByDescending(h => h.HopDongId)
                .Take(5)
                .ToListAsync();

            return View(dsHopDongMoi);
        }
    }
}