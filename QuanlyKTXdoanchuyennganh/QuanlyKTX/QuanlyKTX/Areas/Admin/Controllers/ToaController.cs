using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ToaController : Controller
    {
        private readonly KtxthongminhContext _context;

        public ToaController(KtxthongminhContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View(_context.ToaNhas.ToList());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(ToaNha toaNha)
        {
            if (_context.ToaNhas.Any(x => x.ToaNhaId == toaNha.ToaNhaId))
            {
                TempData["Error"] = $"Mã tòa {toaNha.ToaNhaId} đã tồn tại trong hệ thống!";
                return View(toaNha);
            }

            _context.ToaNhas.Add(toaNha);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Edit(string id)
        {
            return View(_context.ToaNhas.Find(id));
        }

        [HttpPost]
        public IActionResult EditToaNha(ToaNha toaNha)
        {
            _context.Update(toaNha);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Delete(string id)
        {
            ToaNha toaNha = _context.ToaNhas.Find(id);
            _context.Remove(toaNha);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }
    }
}
