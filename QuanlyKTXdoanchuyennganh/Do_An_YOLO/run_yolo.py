import cv2
from ultralytics import YOLO

# 1. Khởi tạo mô hình YOLOv8 bản Nano (bản nhẹ nhất, chạy mượt trên CPU laptop)
# Lần đầu chạy máy sẽ tự động tải file yolov8n.pt về thư mục hiện tại
print("Đang tải model YOLOv8...")
model = YOLO("yolov8n.pt") 

# 2. Bật kết nối với Webcam (số 0 là camera mặc định của laptop)
cap = cv2.VideoCapture(0)

print("Đã kết nối Camera! Bấm phím 'q' trên cửa sổ để thoát.")

# 3. Vòng lặp liên tục để quét từng khung hình (frame) theo thời gian thực
while True:
    # Đọc ảnh từ camera
    success, frame = cap.read()
    if not success:
        print("Không thể đọc được camera!")
        break

    # 4. TRÁI TIM CỦA HỆ THỐNG: Đưa ảnh vào model YOLO để detect
    # Mô hình sẽ trả về tọa độ Bounding Box và tên vật thể
    results = model(frame)

    # 5. Lấy bức ảnh đã được YOLO tự động vẽ khung và dán nhãn
    annotated_frame = results[0].plot()

    # 6. Hiển thị bức ảnh đó lên màn hình
    cv2.imshow("Test YOLOv8 - One Stage Detection", annotated_frame)

    # 7. Nhấn phím 'q' để thoát vòng lặp
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

# Dọn dẹp tài nguyên sau khi tắt
cap.release()
cv2.destroyAllWindows()