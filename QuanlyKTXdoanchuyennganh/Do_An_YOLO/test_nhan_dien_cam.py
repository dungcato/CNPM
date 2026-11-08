import cv2
from deepface import DeepFace
import requests
import urllib3

# Tắt cảnh báo bảo mật lằng nhằng của localhost
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# 1. Đường dây nóng của Web C# (Kiểm tra lại xem đúng cổng 7125 chưa nhé)
URL_WEB = "https://localhost:7125/api/Ai/nhan-dien"

# 2. Ảnh mẫu của mày (Lưu sẵn trong thư mục VS Code xanh)
anh_mau = "anh_goc.jpg"

# 3. Bật Camera
cap = cv2.VideoCapture(0)
print("====================================")
print("🤖 HỆ THỐNG AI KTX ĐÃ KHỞI ĐỘNG!")
print("👉 BẤM PHÍM 'c' để chụp và nhận diện.")
print("👉 BẤM PHÍM 'q' để thoát.")
print("====================================")

while True:
    success, frame = cap.read()
    if not success: break
        
    cv2.imshow("Camera Truoc Cong KTX", frame)
    key = cv2.waitKey(1) & 0xFF
    
    # Khi mày ló mặt vào và bấm 'c'
    if key == ord('c'):
        print("\n⏳ Đang phân tích khuôn mặt...")
        cv2.imwrite("anh_tam_thoi.jpg", frame)
        
        try:
            # Dùng ArcFace để soi mặt
            ket_qua = DeepFace.verify(img1_path = anh_mau, 
                                      img2_path = "anh_tam_thoi.jpg", 
                                      model_name = "ArcFace")
            
            # Chuẩn bị cái hộp để đóng gói dữ liệu gửi đi
            goi_hang = {"MaSinhVien": "", "TrangThai": ""}

            if ket_qua["verified"]:
                print("✅ ĐÚNG NGƯỜI QUEN! Đang gọi điện báo Web C# mở cổng...")
                goi_hang["MaSinhVien"] = "DUNG_IT_01"  # Mã của mày
                goi_hang["TrangThai"] = "Xác nhận đúng người - Đã mở cổng"
            else:
                print("❌ NGƯỜI LẠ! Đang gọi điện báo động cho Web C#...")
                goi_hang["MaSinhVien"] = "KHONG_XAC_DINH"
                goi_hang["TrangThai"] = "CẢNH BÁO XÂM NHẬP!"
            
            # Đạn đã nạp, bóp cò bắn sang Web C#!
            try:
                response = requests.post(URL_WEB, json=goi_hang, verify=False)
                print("💬 Web C# phản hồi:", response.text)
            except Exception as e:
                print("⚠️ Lỗi đứt cáp, không gọi được sang Web C#:", e)

        except Exception as e:
            print("⚠️ Lỗi AI: Không thấy khuôn mặt nào, hãy đứng ra sáng và thử lại!")

    elif key == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()