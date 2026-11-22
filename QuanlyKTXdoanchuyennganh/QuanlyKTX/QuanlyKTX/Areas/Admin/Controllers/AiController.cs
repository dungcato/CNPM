using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanlyKTX.Models;
using Microsoft.AspNetCore.SignalR;
using QuanlyKTX.Hubs;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace QuanlyKTX.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly KtxthongminhContext _context;
        private readonly IHubContext<CameraHub> _hubContext;
        private const string PYTHON = "http://localhost:5000";
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

        public AiController(KtxthongminhContext context, IHubContext<CameraHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("camera-stream")]
        public async Task ProxyStream()
        {
            Response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            Response.Headers["Cache-Control"] = "no-cache";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{PYTHON}/video_feed");
                using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                using var stream = await res.Content.ReadAsStreamAsync();
                var buf = new byte[8192]; int n;
                while ((n = await stream.ReadAsync(buf)) > 0)
                {
                    await Response.Body.WriteAsync(buf.AsMemory(0, n));
                    await Response.Body.FlushAsync();
                }
            }
            catch { }
        }

        [HttpPost("kich-hoat-camera")]
        public async Task<IActionResult> KichHoat()
        {
            try
            {
                var res = await Http.PostAsync($"{PYTHON}/trigger_scan", null);
                return Content(await res.Content.ReadAsStringAsync(), "application/json");
            }
            catch { return StatusCode(503, new { status = "error" }); }
        }

        [HttpGet("danh-sach-ten")]
        public async Task<IActionResult> DanhSachTen()
        {
            var ds = await _context.SinhViens.Select(s => new { mssv = s.Mssv, hoTen = s.HoTen }).ToListAsync();
            return Ok(ds);
        }

        [HttpPost("xac-thuc-khuon-mat")]
        public async Task<IActionResult> XacThuc([FromBody] FaceIDRequest req)
        {
            object resultData;

            if (req.Mssv == "Khong_Xac_Dinh")
            {
                // XỬ LÝ NGƯỜI LẠ
                string fileUrl = "/uploads/strangers/no-image.jpg";
                if (!string.IsNullOrEmpty(req.ImageData))
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(req.ImageData);
                        var fileName = $"kegan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "strangers");
                        Directory.CreateDirectory(folder);
                        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes);
                        fileUrl = $"/uploads/strangers/{fileName}";
                    }
                    catch { }
                }

                var log = new NhatKyAnNinh
                {
                    ThoiGian = DateTime.Now,
                    LoaiSuKien = "Xâm nhập trái phép",
                    DoiTuongNhanDien = "Người lạ",
                    HinhAnhSnapshot = fileUrl,
                    MucDoCanhBao = "Nguy hiểm",
                    TrangThaiXuLy = false,
                    GhiChuXuLy = "Phát Hiện Người lạ Lảng Vảng ở khu vực KTX"
                };
                _context.NhatKyAnNinhs.Add(log);
                await _context.SaveChangesAsync();

                resultData = new
                {
                    status = "Danger",
                    title = "CẢNH BÁO",
                    message = "Người lạ xâm nhập! Đã chụp lại bằng chứng.",
                    img = fileUrl,
                    time = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
                };
            }
            else
            {
                // XỬ LÝ ĐÚNG SINH VIÊN
                var sv = await _context.SinhViens.FirstOrDefaultAsync(s => s.Mssv == req.Mssv);
                resultData = new
                {
                    status = "Success",
                    title = "Xác nhận đúng!",
                    message = $"Mở cửa cho sinh viên:\n{sv?.HoTen} ({sv?.Mssv})",
                    mssv = sv?.Mssv,
                    hoTen = sv?.HoTen
                };
            }

            await _hubContext.Clients.All.SendAsync("NhanKetQuaKhuonMat", resultData);
            return Ok(new { message = "Processed" });
        }

        [HttpGet("lich-su-an-ninh")]
        public async Task<IActionResult> LichSu()
        {
            var logs = await _context.NhatKyAnNinhs
                .OrderByDescending(n => n.ThoiGian)
                .Take(10)
                .Select(n => new
                {
                    thoiGian = n.ThoiGian.HasValue ? n.ThoiGian.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
                    noiDung = n.GhiChuXuLy ?? "Không rõ",
                    hinhAnh = n.HinhAnhSnapshot,
                    mucDo = n.MucDoCanhBao
                }).ToListAsync();
            return Ok(logs);
        }
    }

    public class FaceIDRequest
    {
        public string Mssv { get; set; }
        public string ImageData { get; set; }
    }
}