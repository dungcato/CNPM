using System.Collections.Generic;

namespace QuanlyKTX.Models
{
    public class DashboardViewModel
    {
        // Chứa thông tin sinh viên (Lấy từ bảng SinhVien)
        public SinhVien SinhVien { get; set; }

        // Thông tin vị trí lưu trú thực tế
        public string TenPhong { get; set; }
        public string TenToa { get; set; }

        // Dữ liệu cho các thẻ thống kê (Số dư, Hóa đơn, Phản ánh)
        public decimal TongTienHoaDon { get; set; }
        public int SoPhanAnhCho { get; set; }

        // Danh sách thông báo mới nhất từ Ban quản lý
        public List<ThongBao> DanhSachThongBao { get; set; }

        // Lịch sử ra vào cổng nhận diện AI
        public List<NhatKyAnNinh> LichSuAnNinh { get; set; }

        // [BỔ SUNG QUAN TRỌNG]: Danh sách yêu cầu/phản ánh để hiển thị tiến độ thực tế
        // Phần này sẽ lấy dữ liệu từ bảng PhanAnhs để hiện trạng thái "Đang xử lý", "Hoàn thành"...
        public List<PhanAnh> DanhSachYeuCauGanDay { get; set; }
    }
}