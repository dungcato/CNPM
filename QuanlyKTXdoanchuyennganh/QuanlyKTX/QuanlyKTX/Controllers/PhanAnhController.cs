using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuanlyKTX.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace QuanlyKTX.Controllers
{
    [Authorize(Roles = "SinhVien")]
    public class PhanAnhController : Controller
    {
        private readonly KtxthongminhContext _context;

        public PhanAnhController(KtxthongminhContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GuiPhanAnh() => View();

        [HttpPost]
        public async Task<IActionResult> XuLyPhanAnh(PhanAnh model, IFormFile HinhAnh)
        {
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);
            var sinhVien = _context.SinhViens.FirstOrDefault(s => s.Mssv == mssvHienTai);

            model.Mssv = mssvHienTai;
            model.PhongId = sinhVien?.PhongDangOid;
            model.NgayGui = DateTime.Now;
            model.TrangThai = "Chờ tiếp nhận";

            if (HinhAnh != null && HinhAnh.Length > 0)
            {
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "phananh");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(HinhAnh.FileName);
                string filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await HinhAnh.CopyToAsync(stream);

                model.HinhAnh = "/uploads/phananh/" + fileName;
            }

            _context.PhanAnhs.Add(model);
            await _context.SaveChangesAsync();

            TempData["ShowSuccessModal"] = true;
            return RedirectToAction("GuiPhanAnh");
        }

        [HttpGet]
        public IActionResult TheoDoi()
        {
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);

            var dsPhanAnh = _context.PhanAnhs
                .Where(p => p.Mssv == mssvHienTai)
                .OrderByDescending(p => p.NgayGui)
                .ToList();

            ViewBag.Total = dsPhanAnh.Count;
            ViewBag.Pending = dsPhanAnh.Count(p => p.TrangThai == "Chờ tiếp nhận");
            ViewBag.Processing = dsPhanAnh.Count(p => p.TrangThai == "Đang xử lý");
            ViewBag.Completed = dsPhanAnh.Count(p => p.TrangThai == "Đã hoàn thành");

            return View(dsPhanAnh);
        }
    }
}