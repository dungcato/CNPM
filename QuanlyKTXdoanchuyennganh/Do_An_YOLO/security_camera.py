from flask import Flask, Response
from flask_cors import CORS
import cv2
import numpy as np
from deepface import DeepFace
from ultralytics import YOLO
import requests
import urllib3
import time
import os
import base64
from datetime import datetime

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

app = Flask(__name__)
CORS(app)
camera = cv2.VideoCapture(0)

URL_WEB_API = "https://localhost:44315/api/Ai/xac-thuc-khuon-mat"
THU_MUC_DB_ANH = "DbAnhSinhVien"

THOI_GIAN_LANG_VANG = 15 # Nếu đứng lỳ 15 giây (sau khi xác định là người lạ) sẽ báo động đỏ

print("🧠 Đang tải mô hình YOLOv8 Tracking...")
yolo_model = YOLO("yolov8n.pt") 

kho_vector_khuon_mat = []

# TỪ ĐIỂN QUẢN LÝ TRẠNG THÁI THEO VẾT (Tracking ID)
# Cấu trúc: { track_id: {'status': 'UNCHECKED' | 'SAFE' | 'WARNING', 'first_seen': time, 'alarmed': False} }
track_history = {}

def nap_whitelist():
    print(f"⏳ Đang nạp danh sách Sinh viên an toàn từ thư mục '{THU_MUC_DB_ANH}'...")
    if not os.path.exists(THU_MUC_DB_ANH): os.makedirs(THU_MUC_DB_ANH)
    for ten_file in os.listdir(THU_MUC_DB_ANH):
        if ten_file.lower().endswith(('.png', '.jpg', '.jpeg')):
            mssv = os.path.splitext(ten_file)[0]
            try:
                img = cv2.imread(os.path.join(THU_MUC_DB_ANH, ten_file))
                emb_objs = DeepFace.represent(img_path=img, model_name="ArcFace", enforce_detection=True)
                kho_vector_khuon_mat.append({"mssv": mssv, "vector": emb_objs[0]["embedding"]})
            except: pass
    print(f"🎉 Nạp Whitelist hoàn tất.")

def tinh_cosine(nguon, dich):
    a = np.matmul(np.transpose(nguon), dich)
    b = np.sum(np.multiply(nguon, nguon)) * np.sum(np.multiply(dich, dich))
    return 1 - (a / np.sqrt(b))

nap_whitelist()

def generate_frames():
    global track_history
    
    while True:
        success, frame = camera.read()
        if not success: break
        
        current_time = time.time()
        
        # BƯỚC 1: YOLO TRACKING - Cấp ID cho mọi đối tượng là CON NGƯỜI
        results = yolo_model.track(frame, persist=True, verbose=False, classes=[0])
        
        active_ids = []
        
        if results[0].boxes is not None and results[0].boxes.id is not None:
            boxes = results[0].boxes.xyxy.cpu().numpy().astype(int)
            track_ids = results[0].boxes.id.cpu().numpy().astype(int)
            active_ids = track_ids.tolist()
            
            for box, track_id in zip(boxes, track_ids):
                x1, y1, x2, y2 = box
                
                # Khởi tạo hồ sơ nếu ID mới xuất hiện
                if track_id not in track_history:
                    track_history[track_id] = {
                        'status': 'UNCHECKED',
                        'first_seen': current_time,
                        'alarmed': False
                    }
                
                p_info = track_history[track_id]
                
                # BƯỚC 2: QUÉT MẶT CHỈ 1 LẦN (DEEPFACE) VỚI LOGIC CHỐNG CHE MẶT
                if p_info['status'] == 'UNCHECKED':
                    person_crop = frame[max(0, y1):y2, max(0, x1):x2]
                    if person_crop.size > 0:
                        try:
                            # TÍNH NĂNG MỚI: Ép buộc phải nhìn thấy khuôn mặt mới phân tích
                            emb_objs = DeepFace.represent(img_path=person_crop, model_name="ArcFace", enforce_detection=True)
                            if len(emb_objs) > 0:
                                cam_vector = emb_objs[0]["embedding"]
                                min_dist = 100.0
                                for sv in kho_vector_khuon_mat:
                                    dist = tinh_cosine(sv["vector"], cam_vector)
                                    if dist < min_dist: min_dist = dist
                                
                                if min_dist <= 0.75:
                                    p_info['status'] = 'SAFE'
                                    print(f"ID_{track_id}: Đã xác nhận [SINH VIÊN]. Bỏ qua giám sát.")
                                else:
                                    p_info['status'] = 'WARNING'
                                    p_info['first_seen'] = current_time # Bắt đầu đếm giờ lảng vảng cho người lạ
                                    print(f"ID_{track_id}: [NGƯỜI LẠ] Bắt đầu theo dõi hành vi!")
                        except:
                            pass # Không thấy mặt (quay lưng/bị mờ) -> Bỏ qua chờ khung hình tiếp theo
                    
                    # LOGIC XỬ LÝ KẺ GIAN CHE MẶT:
                    # Nếu lảng vảng quá 10s mà camera vẫn chưa bắt được mặt -> Chuyển thành KHẢ NGHI luôn
                    if p_info['status'] == 'UNCHECKED' and (current_time - p_info['first_seen'] > 10):
                        p_info['status'] = 'WARNING'
                        p_info['first_seen'] = current_time # Bắt đầu đếm 15s cảnh báo
                        print(f"ID_{track_id}: [KHẢ NGHI] Cố tình che mặt quá 10s -> Đưa vào danh sách đen!")
                
                # BƯỚC 3: XỬ LÝ LOGIC VÀ VẼ LÊN MÀN HÌNH
                if p_info['status'] == 'SAFE':
                    # Sinh viên an toàn -> Khung Xanh, Không đếm giờ lảng vảng
                    cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
                    cv2.putText(frame, f"ID_{track_id} [SAFE]", (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
                
                elif p_info['status'] == 'WARNING':
                    time_stayed = int(current_time - p_info['first_seen'])
                    
                    if not p_info['alarmed']:
                        if time_stayed >= THOI_GIAN_LANG_VANG:
                            # HẾT 15 GIÂY -> BÁO ĐỘNG ĐỎ VÀ CHỤP ẢNH
                            p_info['alarmed'] = True
                            print(f"🚨 BÁO ĐỘNG: ID_{track_id} lảng vảng quá thời gian!")
                            
                            # Tách ảnh chụp bằng chứng
                            alert_img = frame.copy()
                            cv2.rectangle(alert_img, (x1, y1), (x2, y2), (0, 0, 255), 3)
                            t = datetime.now().strftime("%d/%m/%Y %H:%M:%S")
                            cv2.putText(alert_img, f"LOITERING SUSPECT ({t})", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 0, 255), 2)
                            
                            _, buffer = cv2.imencode('.jpg', alert_img)
                            base64_str = base64.b64encode(buffer).decode('utf-8')
                            
                            # Gửi API về C# 
                            try: requests.post(URL_WEB_API, json={"Mssv": "Khong_Xac_Dinh", "ImageData": base64_str}, verify=False, timeout=3)
                            except: pass
                        else:
                            # Đang đếm ngược cảnh báo lảng vảng
                            cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 165, 255), 2)
                            cv2.putText(frame, f"ID_{track_id} [?] - {time_stayed}s", (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 165, 255), 2)
                    else:
                        # Đã báo động xong -> Chớp đỏ liên tục
                        cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 0, 255), 3)
                        cv2.putText(frame, f"ID_{track_id} [THREAT]", (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 0, 255), 2)

                else: 
                    # Trạng thái UNCHECKED (Khởi tạo, đang rình chụp mặt)
                    cv2.rectangle(frame, (x1, y1), (x2, y2), (255, 255, 0), 2)
                    cv2.putText(frame, f"ID_{track_id} [Scanning...]", (x1, y1 - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 0), 2)

        # XÓA RÁC BỘ NHỚ (Người rời khỏi cam thì tự động reset dữ liệu)
        keys_to_delete = [tid for tid in track_history.keys() if tid not in active_ids]
        for tid in keys_to_delete:
            del track_history[tid]

        cv2.putText(frame, "CAM-02: HANH LANG KTX (LIVE)", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 255, 255), 2)
        ret, buffer = cv2.imencode('.jpg', frame)
        yield (b'--frame\r\n' b'Content-Type: image/jpeg\r\n\r\n' + buffer.tobytes() + b'\r\n')

@app.route('/video_security_feed')
def video_security_feed():
    return Response(generate_frames(), mimetype='multipart/x-mixed-replace; boundary=frame')

if __name__ == '__main__':
    print("=========================================================")
    print("🛡️ TRẠM AI GIÁM SÁT AN NINH ")
    print("=========================================================")
    app.run(host='0.0.0.0', port=5001, debug=False, use_reloader=False)