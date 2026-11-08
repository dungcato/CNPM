using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize] // Bắt buộc đăng nhập
    public class HoaDonController : Controller
    {
        private readonly KtxthongminhContext _context;

        public HoaDonController(KtxthongminhContext context)
        {
            _context = context;
        }

        // ==============================================================
        // THIẾT LẬP AN NINH: CHỈ ADMIN (VAI TRÒ 1) MỚI ĐƯỢC VÀO
        // ==============================================================
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var vaiTroClaim = User.FindFirst("VaiTroId")?.Value;

            // Nếu không phải là vai trò 1 thì chặn đứng ngay lập tức
            if (vaiTroClaim != "1")
            {
                // 1. Xóa sạch thông báo lỗi cũ từ các Controller khác để tránh hiện sai nội dung
                TempData.Remove("Error");

                // 2. Thiết lập thông báo chuẩn xác cho mục Hóa đơn
                TempData["Error"] = "Khu vực nhạy cảm! Chỉ Quản trị viên hệ thống (Admin) mới có quyền truy cập Hóa đơn.";

                // 3. Đá về trang chủ Admin
                context.Result = RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            base.OnActionExecuting(context);
        }

        // 1. DANH SÁCH HÓA ĐƠN
        [HttpGet]
        public async Task<IActionResult> Index(string searchPhong, int? searchThang, int? searchNam, string searchTrangThai)
        {
            try
            {
                var query = _context.HoaDons
                    .Include(h => h.MssvNavigation)
                    .Include(h => h.Phong)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchPhong))
                    query = query.Where(h => h.PhongId != null && h.PhongId.Contains(searchPhong));

                if (searchThang.HasValue)
                    query = query.Where(h => h.Thang == searchThang.Value);

                if (searchNam.HasValue)
                    query = query.Where(h => h.Nam == searchNam.Value);

                if (!string.IsNullOrEmpty(searchTrangThai))
                    query = query.Where(h => h.TrangThai == searchTrangThai);

                var danhSach = await query
                    .OrderByDescending(h => h.Nam ?? 0)
                    .ThenByDescending(h => h.Thang ?? 0)
                    .ToListAsync();

                ViewBag.SearchPhong = searchPhong;
                ViewBag.SearchThang = searchThang;
                ViewBag.SearchNam = searchNam;
                ViewBag.SearchTrangThai = searchTrangThai;

                return View(danhSach);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi khi tải danh sách hóa đơn: " + ex.Message;
                return View(new List<HoaDon>());
            }
        }

        // 2. FORM TẠO HÓA ĐƠN
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.PhongList = _context.Phongs.ToList();
            ViewBag.SinhVienList = _context.SinhViens.Where(s => s.TrangThai == "Đang lưu trú").ToList();

            ViewBag.AutoMaHD = "INV-" + DateTime.Now.ToString("MMyy-HHmm");
            ViewBag.ThangHienTai = DateTime.Now.Month;
            ViewBag.NamHienTai = DateTime.Now.Year;

            return View();
        }

        // 3. XỬ LÝ LƯU HÓA ĐƠN (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HoaDon hd)
        {
            try
            {
                bool daTonTai = await _context.HoaDons.AnyAsync(h => h.PhongId == hd.PhongId && h.Thang == hd.Thang && h.Nam == hd.Nam);
                if (daTonTai)
                {
                    TempData["Error"] = $"Phòng {hd.PhongId} đã được xuất hóa đơn trong Tháng {hd.Thang}/{hd.Nam}. Không thể tạo trùng!";
                    return RedirectToAction(nameof(Create));
                }

                hd.TongTien = (hd.TienPhong ?? 0) + (hd.TienDien ?? 0) + (hd.TienNuoc ?? 0) + (hd.PhiDichVu ?? 0);

                if (hd.TrangThai == "Đã thanh toán")
                {
                    hd.NgayThanhToan = DateTime.Now;
                }

                _context.HoaDons.Add(hd);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xuất hóa đơn thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tạo hóa đơn: " + ex.Message;
                return RedirectToAction(nameof(Create));
            }
        }

        // 4. XÁC NHẬN THANH TOÁN NHANH
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, string phuongThuc)
        {
            var hd = await _context.HoaDons.FindAsync(id);
            if (hd != null)
            {
                hd.TrangThai = "Đã thanh toán";
                hd.NgayThanhToan = DateTime.Now;
                hd.PhuongThucTt = phuongThuc;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã thu tiền hóa đơn {hd.MaHoaDon} thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // 5. XÓA HÓA ĐƠN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var hd = await _context.HoaDons.FindAsync(id);
            if (hd != null)
            {
                _context.HoaDons.Remove(hd);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa hóa đơn!";
            }
            return RedirectToAction(nameof(Index));
        }

        // 6. FORM CẬP NHẬT & CHI TIẾT
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var hd = await _context.HoaDons
                .Include(h => h.MssvNavigation)
                .Include(h => h.Phong)
                .FirstOrDefaultAsync(h => h.HoaDonId == id);

            if (hd == null) return NotFound();

            ViewBag.PhongList = await _context.Phongs.ToListAsync();
            ViewBag.SinhVienList = await _context.SinhViens.Where(s => s.TrangThai == "Đang lưu trú").ToListAsync();

            return View(hd);
        }

        // 7. XỬ LÝ CẬP NHẬT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HoaDon hd)
        {
            try
            {
                var hdDb = await _context.HoaDons.FindAsync(hd.HoaDonId);
                if (hdDb == null) return NotFound();

                hdDb.Mssv = hd.Mssv;
                hdDb.PhongId = hd.PhongId;
                hdDb.Thang = hd.Thang;
                hdDb.Nam = hd.Nam;
                hdDb.HanThanhToan = hd.HanThanhToan;
                hdDb.GhiChu = hd.GhiChu;
                hdDb.TienPhong = hd.TienPhong;
                hdDb.TienDien = hd.TienDien;
                hdDb.TienNuoc = hd.TienNuoc;
                hdDb.PhiDichVu = hd.PhiDichVu;
                hdDb.TongTien = (hd.TienPhong ?? 0) + (hd.TienDien ?? 0) + (hd.TienNuoc ?? 0) + (hd.PhiDichVu ?? 0);

                if (hd.TrangThai == "Đã thanh toán" && hdDb.TrangThai != "Đã thanh toán")
                {
                    hdDb.NgayThanhToan = DateTime.Now;
                    hdDb.PhuongThucTt = hd.PhuongThucTt ?? "Tiền mặt";
                }
                else if (hd.TrangThai != "Đã thanh toán")
                {
                    hdDb.NgayThanhToan = null;
                    hdDb.PhuongThucTt = null;
                }

                hdDb.TrangThai = hd.TrangThai;

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật Hóa đơn {hdDb.MaHoaDon} thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi cập nhật hóa đơn: " + ex.Message;
                return RedirectToAction(nameof(Edit), new { id = hd.HoaDonId });
            }
        }

        // 8. GỬI NHẮC NHỞ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remind(int id)
        {
            var hd = await _context.HoaDons
                .Include(h => h.MssvNavigation)
                .FirstOrDefaultAsync(h => h.HoaDonId == id);

            if (hd != null)
            {
                string tenSV = hd.MssvNavigation?.HoTen ?? "Sinh viên";
                string emailSV = hd.MssvNavigation?.Email;

                if (!string.IsNullOrEmpty(emailSV))
                {
                    TempData["Success"] = $"Đã lệnh gửi nhắc nợ thành công đến sinh viên {tenSV}.";
                }
                else
                {
                    TempData["Error"] = $"Sinh viên {tenSV} chưa có email trong hệ thống.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}