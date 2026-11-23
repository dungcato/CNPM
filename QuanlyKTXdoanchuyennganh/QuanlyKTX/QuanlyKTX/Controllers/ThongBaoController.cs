using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;
using System;
using System.Linq;
using System.Security.Claims;

namespace QuanlyKTX.Controllers
{
    [Authorize(Roles = "SinhVien")]
    public class ThongBaoController : Controller
    {
        private readonly KtxthongminhContext _context;

        public ThongBaoController(KtxthongminhContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var listThongBao = _context.ThongBaos
                .OrderByDescending(t => t.NgayGui)
                .ToList();

            for (int i = 0; i < listThongBao.Count; i++) 
            {
                SinhVien sinhVien = _context.SinhViens
                    .Where(sinhVien => sinhVien.Mssv == User.FindFirstValue(ClaimTypes.Name))
                    .Include(sinhVien => sinhVien.PhongDangO)
                        .ThenInclude(phong => phong.ToaNha)
                    .FirstOrDefault();

                ThongBao thongBao = listThongBao[i];

                switch (thongBao.DoiTuongNhan)
                {
                    case "Tòa nhà":
                        if (sinhVien.PhongDangO == null || sinhVien.PhongDangO.ToaNha == null || thongBao.ChiTietDoiTuong != sinhVien.PhongDangO.ToaNha.ToaNhaId)
                        {
                            listThongBao.RemoveAt(i);
                            i--;
                        }
                        break;
                    case "Phòng":
                        if (sinhVien.PhongDangO == null || thongBao.ChiTietDoiTuong != sinhVien.PhongDangO.TenPhong)
                        {
                            listThongBao.RemoveAt(i);
                            i--;
                        }
                        break;
                    case "Cá nhân":
                        if (thongBao.ChiTietDoiTuong != sinhVien.Mssv)
                        {
                            listThongBao.RemoveAt(i);
                            i--;
                        }
                        break;
                }
            }

            ViewBag.Total = listThongBao.Count;
            ViewBag.FinanceCount = listThongBao.Count(t => t.LoaiThongBao == "Tài chính");
            ViewBag.EventCount = listThongBao.Count(t => t.LoaiThongBao == "Sự kiện" || t.LoaiThongBao == "Bảo trì");
            ViewBag.UnreadCount = listThongBao.Count(t => t.NgayGui >= DateTime.Now.AddDays(-1));

            return View(listThongBao);
        }

        public IActionResult ChiTiet(int id)
        {
            var thongBao = _context.ThongBaos.FirstOrDefault(t => t.ThongBaoId == id);
            if (thongBao == null) return NotFound();
            return View(thongBao);
        }
    }
}