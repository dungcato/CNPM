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
    [Authorize] // Bắt buộc phải đăng nhập
    public class DienNuocController : Controller
    {
        private readonly KtxthongminhContext _context;

        public DienNuocController(KtxthongminhContext context)
        {
            _context = context;
        }

        // ==============================================================
        // BỘ LỌC QUYỀN HẠN: Chặn VaiTroId = 4 (Nhân viên/Giáo viên)
        // ==============================================================
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var vaiTroClaim = User.FindFirst("VaiTroId")?.Value;

            if (vaiTroClaim == "4")
            {
                // 1. Xóa sạch thông báo lỗi cũ từ các Controller khác để tránh hiện sai nội dung
                TempData.Remove("Error");

                // 2. Gán thông báo lỗi đúng cho trang này
                TempData["Error"] = "Bạn không đủ quyền hạn để truy cập vào mục Ghi nhận Điện nước!";

                // 3. Đá về trang chủ Admin thay vì đứng im tại trang hiện tại
                context.Result = RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            base.OnActionExecuting(context);
        }

        // 1. HIỂN THỊ DANH SÁCH & BỘ LỌC
        [HttpGet]
        public async Task<IActionResult> Index(string searchPhong, string searchToaNha, int? searchThang, int? searchNam)
        {
            int thang = searchThang ?? DateTime.Now.Month;
            int nam = searchNam ?? DateTime.Now.Year;

            var query = _context.DienNuocs
                .Include(d => d.Phong)
                .Where(d => d.Thang == thang && d.Nam == nam)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchPhong))
                query = query.Where(d => d.PhongId != null && d.PhongId.Contains(searchPhong));

            if (!string.IsNullOrEmpty(searchToaNha))
                query = query.Where(d => d.Phong.ToaNhaId == searchToaNha);

            var danhSach = await query.OrderByDescending(d => d.NgayChot).ToListAsync();

            ViewBag.SearchPhong = searchPhong;
            ViewBag.SearchThang = thang;
            ViewBag.SearchNam = nam;

            var toaNhaList = await _context.ToaNhas.ToListAsync();
            ViewBag.ToaNhaList = new SelectList(toaNhaList, "ToaNhaId", "ToaNhaId", searchToaNha);

            ViewBag.DanhSachDaTaoHoaDon = await _context.HoaDons
                .Where(h => h.Thang == thang && h.Nam == nam)
                .Select(h => h.PhongId)
                .ToListAsync();

            return View(danhSach);
        }

        // 2. NHẬP SỐ MỚI THỦ CÔNG
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.PhongList = _context.Phongs.OrderBy(p => p.PhongId).ToList();
            ViewBag.ThangHienTai = DateTime.Now.Month;
            ViewBag.NamHienTai = DateTime.Now.Year;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DienNuoc dn, bool taoHoaDon = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dn.PhongId))
                {
                    TempData["Error"] = "Vui lòng chọn phòng cần ghi nhận!";
                    ViewBag.PhongList = _context.Phongs.OrderBy(p => p.PhongId).ToList();
                    ViewBag.ThangHienTai = dn.Thang;
                    ViewBag.NamHienTai = dn.Nam;
                    return View(dn);
                }

                bool daTonTai = await _context.DienNuocs.AnyAsync(d => d.PhongId == dn.PhongId && d.Thang == dn.Thang && d.Nam == dn.Nam);
                if (daTonTai)
                {
                    TempData["Error"] = $"Phòng {dn.PhongId} đã có chỉ số trong kỳ {dn.Thang}/{dn.Nam}. Vui lòng chọn biểu tượng 'Sửa' trong danh sách!";
                    return RedirectToAction(nameof(Index), new { searchThang = dn.Thang, searchNam = dn.Nam });
                }

                if (dn.DienCu == null || dn.NuocCu == null)
                {
                    int prevThang = dn.Thang == 1 ? 12 : dn.Thang - 1;
                    int prevNam = dn.Thang == 1 ? dn.Nam - 1 : dn.Nam;
                    var prevDN = await _context.DienNuocs.FirstOrDefaultAsync(d => d.PhongId == dn.PhongId && d.Thang == prevThang && d.Nam == prevNam);
                    dn.DienCu ??= prevDN?.DienMoi ?? 0;
                    dn.NuocCu ??= prevDN?.NuocMoi ?? 0;
                }

                dn.DonGiaDien ??= 3500;
                dn.DonGiaNuoc ??= 15000;
                dn.NguonNhap = "Thủ công";
                dn.NgayChot = DateTime.Now;

                _context.DienNuocs.Add(dn);
                await _context.SaveChangesAsync();

                if (taoHoaDon)
                {
                    bool hdTonTai = await _context.HoaDons.AnyAsync(h => h.PhongId == dn.PhongId && h.Thang == dn.Thang && h.Nam == dn.Nam);
                    if (!hdTonTai)
                    {
                        decimal tienDien = Math.Max(0, (dn.DienMoi ?? 0) - (dn.DienCu ?? 0)) * (dn.DonGiaDien ?? 3500);
                        decimal tienNuoc = Math.Max(0, (dn.NuocMoi ?? 0) - (dn.NuocCu ?? 0)) * (dn.DonGiaNuoc ?? 15000);

                        var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.PhongDangOid == dn.PhongId && s.TrangThai == "Đang lưu trú");

                        var newHd = new HoaDon
                        {
                            MaHoaDon = "INV-" + DateTime.Now.ToString("MMyyHHmm") + "-" + dn.PhongId,
                            PhongId = dn.PhongId,
                            Mssv = sv?.Mssv,
                            Thang = dn.Thang,
                            Nam = dn.Nam,
                            TienDien = tienDien,
                            TienNuoc = tienNuoc,
                            TienPhong = 0,
                            PhiDichVu = 0,
                            TongTien = tienDien + tienNuoc,
                            TrangThai = "Chưa thanh toán",
                            GhiChu = "Phát hành từ nhập chỉ số thủ công."
                        };

                        _context.HoaDons.Add(newHd);
                        await _context.SaveChangesAsync();
                        TempData["Success"] = $"Đã lưu chỉ số và phát hành Hóa đơn cho phòng {dn.PhongId}!";
                    }
                    else
                    {
                        TempData["Success"] = $"Đã lưu chỉ số phòng {dn.PhongId} (Phòng đã có hóa đơn kỳ này trước đó).";
                    }
                }
                else
                {
                    TempData["Success"] = $"Đã lưu chỉ số điện nước phòng {dn.PhongId} thành công!";
                }

                return RedirectToAction(nameof(Index), new { searchThang = dn.Thang, searchNam = dn.Nam });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // 3. CẬP NHẬT CHỈ SỐ
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || id <= 0)
            {
                TempData["Error"] = "Vui lòng chọn một phòng cụ thể trong danh sách để chỉnh sửa chỉ số!";
                return RedirectToAction(nameof(Index));
            }

            var dn = await _context.DienNuocs.FindAsync(id.Value);
            if (dn == null)
            {
                TempData["Error"] = $"Không tìm thấy dữ liệu chỉ số điện nước (ID: {id})!";
                return RedirectToAction(nameof(Index));
            }

            bool daCoBill = await _context.HoaDons.AnyAsync(h => h.PhongId == dn.PhongId && h.Thang == dn.Thang && h.Nam == dn.Nam);
            ViewBag.DaCoHoaDon = daCoBill;

            return View(dn);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DienNuoc dn)
        {
            try
            {
                var dbObj = await _context.DienNuocs.FindAsync(dn.DienNuocId);
                if (dbObj == null)
                {
                    TempData["Error"] = "Không tìm thấy dữ liệu chỉ số cần cập nhật!";
                    return RedirectToAction(nameof(Index));
                }

                dbObj.DienCu = dn.DienCu;
                dbObj.DienMoi = dn.DienMoi;
                dbObj.NuocCu = dn.NuocCu;
                dbObj.NuocMoi = dn.NuocMoi;
                dbObj.DonGiaDien = dn.DonGiaDien;
                dbObj.DonGiaNuoc = dn.DonGiaNuoc;
                dbObj.NguonNhap = "Cập nhật thủ công";
                dbObj.NgayChot = DateTime.Now;

                var hd = await _context.HoaDons.FirstOrDefaultAsync(h => h.PhongId == dbObj.PhongId && h.Thang == dbObj.Thang && h.Nam == dbObj.Nam);
                if (hd != null)
                {
                    decimal tienDien = Math.Max(0, (dbObj.DienMoi ?? 0) - (dbObj.DienCu ?? 0)) * (dbObj.DonGiaDien ?? 3500);
                    decimal tienNuoc = Math.Max(0, (dbObj.NuocMoi ?? 0) - (dbObj.NuocCu ?? 0)) * (dbObj.DonGiaNuoc ?? 15000);
                    hd.TienDien = tienDien;
                    hd.TienNuoc = tienNuoc;
                    hd.TongTien = (hd.TienPhong ?? 0) + tienDien + tienNuoc + (hd.PhiDichVu ?? 0);
                    _context.HoaDons.Update(hd);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã cập nhật chỉ số cho phòng {dbObj.PhongId}!";
                return RedirectToAction(nameof(Index), new { searchThang = dbObj.Thang, searchNam = dbObj.Nam });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi cập nhật: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // 4. TẠO HÓA ĐƠN ĐỒNG LOẠT
        [HttpGet]
        [ActionName("TaoHoaDonHangLoat")]
        public IActionResult TaoHoaDonHangLoatGet(int? thang, int? nam)
        {
            // Tránh lỗi 404 / 405 nếu người dùng truy cập trực tiếp bằng URL GET hoặc refresh trình duyệt
            int t = (thang.HasValue && thang.Value > 0) ? thang.Value : DateTime.Now.Month;
            int n = (nam.HasValue && nam.Value > 0) ? nam.Value : DateTime.Now.Year;
            return RedirectToAction(nameof(Index), new { searchThang = t, searchNam = n });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TaoHoaDonHangLoat(int? thang, int? nam)
        {
            int t = (thang.HasValue && thang.Value > 0) ? thang.Value : DateTime.Now.Month;
            int n = (nam.HasValue && nam.Value > 0) ? nam.Value : DateTime.Now.Year;

            try
            {
                var listChiSo = await _context.DienNuocs
                    .Where(d => d.Thang == t && d.Nam == n)
                    .ToListAsync();

                if (listChiSo == null || !listChiSo.Any())
                {
                    TempData["Error"] = $"Kỳ {t}/{n} chưa có chỉ số điện nước nào được ghi nhận. Vui lòng nhập chỉ số trước khi chốt sổ tạo hóa đơn!";
                    return RedirectToAction(nameof(Index), new { searchThang = t, searchNam = n });
                }

                int count = 0;
                foreach (var dn in listChiSo)
                {
                    var hdTonTai = await _context.HoaDons.AnyAsync(h => h.PhongId == dn.PhongId && h.Thang == dn.Thang && h.Nam == dn.Nam);

                    if (!hdTonTai)
                    {
                        decimal tienDien = Math.Max(0, (dn.DienMoi ?? 0) - (dn.DienCu ?? 0)) * (dn.DonGiaDien ?? 3500);
                        decimal tienNuoc = Math.Max(0, (dn.NuocMoi ?? 0) - (dn.NuocCu ?? 0)) * (dn.DonGiaNuoc ?? 15000);

                        var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.PhongDangOid == dn.PhongId && s.TrangThai == "Đang lưu trú");

                        var newHd = new HoaDon
                        {
                            MaHoaDon = "INV-" + DateTime.Now.ToString("MMyyHHmm") + "-" + dn.PhongId + "-" + count,
                            PhongId = dn.PhongId,
                            Mssv = sv?.Mssv,
                            Thang = dn.Thang,
                            Nam = dn.Nam,
                            TienDien = tienDien,
                            TienNuoc = tienNuoc,
                            TienPhong = 0,
                            PhiDichVu = 0,
                            TongTien = tienDien + tienNuoc,
                            TrangThai = "Chưa thanh toán",
                            GhiChu = "Phát hành đồng loạt từ hệ thống."
                        };

                        _context.HoaDons.Add(newHd);
                        count++;
                    }
                }

                if (count > 0)
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Đã phát hành đồng loạt {count} Hóa Đơn thành công cho kỳ {t}/{n}!";
                }
                else
                {
                    TempData["Error"] = $"Tất cả các phòng có chỉ số trong kỳ {t}/{n} đều đã có hóa đơn từ trước.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tạo hóa đơn đồng loạt: " + ex.Message;
            }

            return RedirectToAction(nameof(Index), new { searchThang = t, searchNam = n });
        }

        // 5. HỦY CHỈ SỐ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var dn = await _context.DienNuocs.FindAsync(id);
                if (dn != null)
                {
                    _context.DienNuocs.Remove(dn);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Đã hủy bản ghi chỉ số!";
                }
                else
                {
                    TempData["Error"] = "Không tìm thấy bản ghi chỉ số cần xóa.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xóa: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
