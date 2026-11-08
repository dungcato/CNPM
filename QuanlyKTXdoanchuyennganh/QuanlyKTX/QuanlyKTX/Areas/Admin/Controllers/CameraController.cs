using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
  
    public class CameraController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}