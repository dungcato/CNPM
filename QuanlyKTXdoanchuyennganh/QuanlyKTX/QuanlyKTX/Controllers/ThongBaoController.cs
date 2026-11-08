using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanlyKTX.Models;
using System;
using System.Linq;

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