using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize] // Bắt buộc phải đăng nhập mới được vào
    public class KhachController : Controller
    {
        private readonly KtxthongminhContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public KhachController(KtxthongminhContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==============================================================
        // BỘ LỌC QUYỀN HẠN: Chặn VaiTroId = 4 (Nhân viên/Giáo viên)
        // ==============================================================
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Lấy VaiTroId từ Claim đã lưu khi đăng nhập
            var vaiTroClaim = User.FindFirst("VaiTroId")?.Value;

            if (vaiTroClaim == "4")
            {
                // 1. Xóa sạch các thông báo Error cũ từ các mục khác để tránh hiện nhầm nội dung
                TempData.Remove("Error");

                // 2. Thiết lập thông báo chuẩn cho mục Khách thăm
                TempData["Error"] = "Bạn không đủ quyền hạn để truy cập vào mục Quản lý Khách thăm!";

                // 3. Đẩy về trang chủ Admin
                context.Result = RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            base.OnActionExecuting(context);
        }

        // =======================================================
        // 1. HIỂN THỊ DANH SÁCH & BỘ LỌC TÌM KIẾM
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Index(string searchTen, string searchPhong, string searchNgay)
        {
            var query = _context.Khaches
                .Include(k => k.MssvBaoLanhNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTen))
                query = query.Where(k => k.HoTen.Contains(searchTen) || k.Cccd.Contains(searchTen));

            if (!string.IsNullOrEmpty(searchPhong))
                query = query.Where(k => k.MssvBaoLanh.Contains(searchPhong) || k.PhongThamId.Contains(searchPhong));

            var danhSach = await query
                .OrderBy(k => k.TrangThai == "Đang trong KTX" ? 0 : 1)
                .ThenByDescending(k => k.ThoiGianVaoThucTe)
                .ToListAsync();

            return View(danhSach);
        }

        // =======================================================
        // 2. ĐĂNG KÝ KHÁCH MỚI & LƯU ẢNH NHẬN DIỆN (FACE ID)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DangKyKhach(string hoTen, string cccd, string sdt, string nguoiBaoLanh, DateTime? thoiGianRa, string lyDo, string faceImageData, IFormFile uploadedImage, string anhElfinder)
        {
            try
            {
                var sinhVien = await _context.SinhViens
                    .FirstOrDefaultAsync(s => s.Mssv == nguoiBaoLanh || s.PhongDangOid == nguoiBaoLanh);

                if (sinhVien == null)
                {
                    TempData["Error"] = $"Không tìm thấy Sinh viên hoặc Phòng nào có mã '{nguoiBaoLanh}' để bảo lãnh!";
                    return RedirectToAction(nameof(Index));
                }

                string imagePath = "";
                string uploadFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "faceid");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                if (!string.IsNullOrEmpty(faceImageData))
                {
                    string base64Data = faceImageData.Split(',')[1];
                    byte[] imageBytes = Convert.FromBase64String(base64Data);
                    string fileName = $"FaceID_{cccd}_{DateTime.Now:ddMMyyHHmmss}.jpg";
                    string filePath = Path.Combine(uploadFolder, fileName);
                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    imagePath = $"/uploads/faceid/{fileName}";
                }
                else if (uploadedImage != null && uploadedImage.Length > 0)
                {
                    string fileName = $"FaceID_{cccd}_{DateTime.Now:ddMMyyHHmmss}{Path.GetExtension(uploadedImage.FileName)}";
                    string filePath = Path.Combine(uploadFolder, fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await uploadedImage.CopyToAsync(fileStream);
                    }
                    imagePath = $"/uploads/faceid/{fileName}";
                }
                else if (!string.IsNullOrEmpty(anhElfinder))
                {
                    imagePath = anhElfinder;
                }

                var khachMoi = new Khach
                {
                    HoTen = hoTen,
                    Cccd = cccd,
                    Sdt = sdt,
                    MssvBaoLanh = sinhVien.Mssv,
                    PhongThamId = sinhVien.PhongDangOid,
                    LyDo = lyDo,
                    ThoiGianVaoThucTe = DateTime.Now,
                    ThoiGianRaDuKien = thoiGianRa,
                    TrangThai = "Đang trong KTX",
                    GhiChu = string.IsNullOrEmpty(imagePath) ? "Không có ảnh nhận diện" : $"[FACE_ID]:{imagePath}"
                };

                _context.Khaches.Add(khachMoi);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Đăng ký thành công khách {hoTen}! Cấp quyền vào phòng {sinhVien.PhongDangOid}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Đã xảy ra lỗi hệ thống: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // 3. CHECK-OUT
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOutKhach(int id)
        {
            var khach = await _context.Khaches.FindAsync(id);
            if (khach == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin khách này!";
                return RedirectToAction(nameof(Index));
            }

            if (khach.TrangThai == "Đã ra")
            {
                TempData["Error"] = "Khách này đã được Check-out rồi!";
                return RedirectToAction(nameof(Index));
            }

            khach.ThoiGianRaThucTe = DateTime.Now;
            khach.TrangThai = "Đã ra";

            _context.Khaches.Update(khach);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã Check-out cho khách {khach.HoTen} ra khỏi KTX.";
            return RedirectToAction(nameof(Index));
        }

        // =======================================================
        // 4. XÓA BẢN GHI
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var khach = await _context.Khaches.FindAsync(id);
            if (khach != null)
            {
                _context.Khaches.Remove(khach);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa lịch sử khách thăm!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}