using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanlyKTX.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanlyKTX.Controllers
{
    [Authorize(Roles = "SinhVien")]
    public class PhongController : Controller
    {
        private readonly KtxthongminhContext _context;

        public PhongController(KtxthongminhContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult DangKyChuyenPhong()
        {
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);

            var dsYeuCau = _context.PhanAnhs
                .Where(p => p.Mssv == mssvHienTai && p.LoaiSuCo == "Chuyển phòng")
                .OrderByDescending(p => p.NgayGui)
                .ToList();

            return View(dsYeuCau);
        }

        [HttpPost]
        public async Task<IActionResult> XuLyChuyenPhong(string KhuVucMongMuon, string LoaiPhongMongMuon, string LyDoChuyen, string PhongCuThe)
        {
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);
            var sv = _context.SinhViens.FirstOrDefault(s => s.Mssv == mssvHienTai);

            if (!string.IsNullOrEmpty(PhongCuThe))
            {
                var phongTarget = _context.Phongs.FirstOrDefault(p => p.PhongId == PhongCuThe);

                if (phongTarget == null)
                {
                    TempData["ThongBao"] = $"Lỗi: Phòng {PhongCuThe} không tồn tại!";
                    return RedirectToAction("DangKyChuyenPhong");
                }

                // Cập nhật số người tối đa dựa trên LoaiPhongId mới
                int soNguoiMax = phongTarget.LoaiPhongId switch
                {
                    1 => 8,
                    2 => 6,
                    3 => 4,
                    4 => 2,
                    5 => 2,
                    _ => 0
                };

                // Kiểm tra trạng thái "Còn trống" và giới hạn số người
                if (phongTarget.TrangThai != "Còn trống" || phongTarget.SoNguoiHienTai >= soNguoiMax)
                {
                    string lyDo = (phongTarget.TrangThai != "Còn trống")
                        ? $"đang ở trạng thái '{phongTarget.TrangThai}'"
                        : "đã đủ người";

                    TempData["ThongBao"] = $"Lỗi: Không thể đăng ký vào phòng {PhongCuThe} vì phòng {lyDo} ({phongTarget.SoNguoiHienTai}/{soNguoiMax})!";
                    return RedirectToAction("DangKyChuyenPhong");
                }
            }

            var pa = new PhanAnh
            {
                Mssv = mssvHienTai,
                PhongId = sv?.PhongDangOid,
                LoaiSuCo = "Chuyển phòng",
                TieuDe = "Đăng ký chuyển sang " + KhuVucMongMuon,
                NoiDung = $"Nguyện vọng: {LoaiPhongMongMuon}. Mã phòng muốn ghép: {PhongCuThe}. Lý do: {LyDoChuyen}",
                NgayGui = DateTime.Now,
                TrangThai = "Chờ tiếp nhận"
            };

            _context.PhanAnhs.Add(pa);
            await _context.SaveChangesAsync();

            TempData["ThongBao"] = "Thành công! Yêu cầu chuyển phòng đã được gửi tới Ban quản lý.";
            return RedirectToAction("DangKyChuyenPhong");
        }

        [HttpGet]
        public IActionResult DangKyThue()
        {
            ViewBag.loaiPhongList = _context.LoaiPhongs.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> XuLyDangKy(string MSSV, int LoaiPhongId, DateTime NgayBatDau, int ThoiHanThang)
        {
            var pa = new PhanAnh
            {
                Mssv = MSSV,
                PhongId = null,
                LoaiSuCo = "Đăng ký phòng",
                TieuDe = "",
                NoiDung = $"{LoaiPhongId}#{NgayBatDau}#{ThoiHanThang}",
                NgayGui = DateTime.Now,
                TrangThai = "Chờ tiếp nhận"
            };

            _context.PhanAnhs.Add(pa);
            await _context.SaveChangesAsync();
            TempData["ThongBao"] = "Thành công! Yêu cầu đăng ký phòng đã được gửi tới Ban quản lý.";
            return RedirectToAction("DangKyThue");
        }
    }
}