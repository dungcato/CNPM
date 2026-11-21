using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CanhBaoController : Controller
    {
        private readonly KtxthongminhContext _context;

        public CanhBaoController(KtxthongminhContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // API AJAX: Tải danh sách sự cố
        [HttpGet]
        public async Task<IActionResult> LayDanhSachSuCo()
        {
            try
            {
                var data = await _context.NhatKyAnNinhs
                    .OrderByDescending(x => x.ThoiGian)
                    .Take(20) // Hiển thị 20 vụ gần nhất
                    .Select(x => new {
                        id = x.LogId, // Chuẩn theo model NhatKyAnNinh.cs
                        thoiGian = x.ThoiGian.HasValue ? x.ThoiGian.Value.ToString("dd/MM/yyyy") : "",
                        gio = x.ThoiGian.HasValue ? x.ThoiGian.Value.ToString("HH:mm:ss") : "",
                        noiDung = x.GhiChuXuLy ?? x.LoaiSuKien ?? "Cảnh báo an ninh",
                        hinhAnh = x.HinhAnhSnapshot ?? "/images/no-image.png",
                        mucDo = x.MucDoCanhBao ?? "Nguy hiểm",
                        trangThai = x.TrangThaiXuLy ?? false // Đã xử lý hay chưa
                    }).ToListAsync();

                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // API AJAX: Bấm nút xác nhận "Đã xử lý"
        [HttpPost]
        public async Task<IActionResult> XuLySuCo(int id)
        {
            try
            {
                var log = await _context.NhatKyAnNinhs.FindAsync(id);
                if (log != null)
                {
                    log.TrangThaiXuLy = true; // Đánh dấu đã giải quyết an toàn
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Đã cập nhật trạng thái sự cố!" });
                }
                return Json(new { success = false, message = "Không tìm thấy sự cố." });
            }
            catch
            {
                return Json(new { success = false, message = "Lỗi hệ thống." });
            }
        }
    }
}