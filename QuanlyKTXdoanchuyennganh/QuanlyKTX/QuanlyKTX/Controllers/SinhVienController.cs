using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
    public class SinhVienController : Controller
    {
        private readonly KtxthongminhContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public SinhVienController(KtxthongminhContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        public IActionResult HoSo()
        {
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);
            var sv = _context.SinhViens.FirstOrDefault(s => s.Mssv == mssvHienTai);
            if (sv == null) return NotFound();
            return View(sv);
        }

        [HttpPost]
        public async Task<IActionResult> LuuHoSo(SinhVien model, IFormFile AvatarFile, IFormFile CoverFile)
        {
            var mssvHienTai = User.FindFirstValue(ClaimTypes.Name);
            var sv = _context.SinhViens.FirstOrDefault(s => s.Mssv == mssvHienTai);
            if (sv == null) return NotFound();

            sv.HoTen = model.HoTen;
            sv.NgaySinh = model.NgaySinh;
            sv.GioiTinh = model.GioiTinh;
            sv.Sdt = model.Sdt;
            sv.Email = model.Email;
            sv.QueQuan = model.QueQuan;

            async Task<string> UploadFile(IFormFile file, string subFolder)
            {
                if (file == null || file.Length == 0) return null;
                string folderPath = Path.Combine(_hostEnvironment.WebRootPath, "uploads", subFolder);
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                string fileName = sv.Mssv + "_" + DateTime.Now.Ticks + Path.GetExtension(file.FileName);
                string filePath = Path.Combine(folderPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await file.CopyToAsync(stream);
                return "/uploads/" + subFolder + "/" + fileName;
            }

            var coverPath = await UploadFile(CoverFile, "covers");
            if (coverPath != null) sv.FaceIdData = coverPath;

            var avatarPath = await UploadFile(AvatarFile, "avatars");
            if (avatarPath != null) sv.FaceIdData = avatarPath;

            _context.SaveChanges();
            return RedirectToAction("HoSo");
        }
    }
}