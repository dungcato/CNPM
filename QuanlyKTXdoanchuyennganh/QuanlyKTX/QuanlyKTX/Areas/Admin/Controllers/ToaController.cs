using Microsoft.AspNetCore.Mvc;
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
    }
}
