from flask import Flask, Response
import cv2
import numpy as np
from deepface import DeepFace
from ultralytics import YOLO
import requests
import urllib3
import time
import threading
import os
import base64
import unicodedata
from datetime import datetime

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

app = Flask(__name__)
camera = cv2.VideoCapture(0)

URL_WEB_API = "https://localhost:44315/api/Ai/xac-thuc-khuon-mat"
URL_GET_NAMES = "https://localhost:44315/api/Ai/danh-sach-ten"
THU_MUC_DB_ANH = "DbAnhSinhVien"

print("🧠 Đang tải mô hình YOLOv8...")
yolo_model = YOLO("yolov8n.pt") 
face_cascade = cv2.CascadeClassifier(cv2.data.haarcascades + 'haarcascade_frontalface_default.xml')

kho_vector_khuon_mat = []
dict_ten_sv = {}

def xoa_dau(s):
    return ''.join(c for c in unicodedata.normalize('NFD', s) if unicodedata.category(c) != 'Mn')

def lay_danh_sach_ten():
    print("⏳ Đang tải từ điển Tên Sinh Viên từ Web C#...")
    try:
        res = requests.get(URL_GET_NAMES, verify=False, timeout=5)
        if res.status_code == 200:
            for sv in res.json():
                dict_ten_sv[sv['mssv']] = xoa_dau(sv['hoTen'])
            print(f"   ✅ Đã tải danh bạ tên của {len(dict_ten_sv)} sinh viên.")
    except Exception: pass

def nap_du_lieu_thu_cong():
    print(f"⏳ Đang nạp ảnh thủ công từ '{THU_MUC_DB_ANH}' vào RAM...")
    if not os.path.exists(THU_MUC_DB_ANH): os.makedirs(THU_MUC_DB_ANH)
    dem = 0
    for ten_file in os.listdir(THU_MUC_DB_ANH):
        if ten_file.lower().endswith(('.png', '.jpg', '.jpeg')):
            mssv = os.path.splitext(ten_file)[0]
            try:
                img = cv2.imread(os.path.join(THU_MUC_DB_ANH, ten_file))
                emb_objs = DeepFace.represent(img_path=img, model_name="ArcFace", enforce_detection=True)
                kho_vector_khuon_mat.append({"mssv": mssv, "vector": emb_objs[0]["embedding"]})
                dem += 1
            except Exception: pass
    print(f"🎉 Đã nạp Vector FaceID của {dem} sinh viên.")

def tinh_cosine(nguon, dich):
    a = np.matmul(np.transpose(nguon), dich)
    b = np.sum(np.multiply(nguon, nguon)) * np.sum(np.multiply(dich, dich))
    return 1 - (a / np.sqrt(b))

lay_danh_sach_ten()
nap_du_lieu_thu_cong()

is_scanning = False
scan_end_time = 0
draw_box = None 
draw_label = ""
draw_color = (0, 0, 0)
face_evidence = None

@app.route('/trigger_scan', methods=['POST'])
def trigger_scan():
    global is_scanning, scan_end_time, draw_box, draw_label, draw_color, face_evidence
    if not is_scanning:
        is_scanning = True
        scan_end_time = time.time() + 6
        draw_box = None
        draw_label = "Dang quet khuon mat..."
        draw_color = (0, 165, 255)
        face_evidence = None
        return {"status": "activated"}
    return {"status": "busy"}

def ai_processing_thread():
    global is_scanning, scan_end_time, draw_box, draw_label, draw_color, face_evidence
    while True:
        if is_scanning:
            # 1. HẾT 6 GIÂY -> NGƯỜI LẠ
            if time.time() > scan_end_time:
                print("🚫 HẾT GIỜ! BÁO ĐỘNG NGƯỜI LẠ!")
                draw_label = "NGUOI LA - TU CHOI!"
                draw_color = (0, 0, 255)
                
                base64_img = ""
                if face_evidence is not None:
                    img_to_save = face_evidence.copy()
                    
                    # IN THỜI GIAN THỰC LÊN ẢNH LÀM BẰNG CHỨNG
                    current_time = datetime.now().strftime("%d/%m/%Y %H:%M:%S")
                    cv2.putText(img_to_save, f"TIME: {current_time}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
                    cv2.putText(img_to_save, "SECURITY: UNAUTHORIZED", (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
                    
                    # VẼ KHUNG VUÔNG LÊN ẢNH NẾU CÓ
                    if draw_box is not None:
                        x, y, w, h = draw_box
                        cv2.rectangle(img_to_save, (x, y), (x+w, y+h), (0, 0, 255), 3)

                    _, buffer = cv2.imencode('.jpg', img_to_save)
                    base64_img = base64.b64encode(buffer).decode('utf-8')
                
                try: requests.post(URL_WEB_API, json={"Mssv": "Khong_Xac_Dinh", "ImageData": base64_img}, verify=False, timeout=3)
                except: pass
                
                time.sleep(2)
                is_scanning = False
                draw_box = None
                continue

            # 2. TRONG 6 GIÂY -> NHẬN DIỆN
            if face_evidence is not None:
                img_check = face_evidence.copy()
                try:
                    emb_objs = DeepFace.represent(img_path=img_check, model_name="ArcFace", enforce_detection=False)
                    if len(emb_objs) > 0:
                        cam_vector = emb_objs[0]["embedding"]
                        best_mssv, min_dist = None, 100.0
# vẽ khung hình yolo phát hiện mặt
                        for sv in kho_vector_khuon_mat:
                            dist = tinh_cosine(sv["vector"], cam_vector)
                            if dist < min_dist:
                                min_dist, best_mssv = dist, sv["mssv"]

                        if min_dist <= 0.75 and best_mssv:
                            ten_sv = dict_ten_sv.get(best_mssv, best_mssv)
                            draw_label = f"SV: {ten_sv} ({best_mssv})"
                            draw_color = (0, 255, 0)
                            
                            print(f"✅ HỢP LỆ: {ten_sv}. Mở cửa ngay!")
                            try: requests.post(URL_WEB_API, json={"Mssv": best_mssv, "ImageData": ""}, verify=False, timeout=3)
                            except: pass
                            
                            time.sleep(2)
                            is_scanning = False
                            draw_box = None
                        else:
                            draw_label = "Dang nhan dien..."
                            draw_color = (0, 165, 255)
                except: pass
        time.sleep(0.05)

threading.Thread(target=ai_processing_thread, daemon=True).start()

def generate_frames():
    global is_scanning, draw_box, draw_label, draw_color, face_evidence
    while True:
        success, frame = camera.read()
        if not success: break
        
        # CAMERA LUÔN BẬT NHƯNG CHƯA QUÉT AI
        if not is_scanning:
            cv2.putText(frame, "CAMERA KTX 1 ", (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)
            ret, buffer = cv2.imencode('.jpg', frame)
            yield (b'--frame\r\n' b'Content-Type: image/jpeg\r\n\r\n' + buffer.tobytes() + b'\r\n')
            time.sleep(0.05) 
            continue
        
        # KHI BẤM NÚT -> KÍCH HOẠT QUÉT BẰNG YOLO & HAAR CASCADE
        results = yolo_model(frame, verbose=False)
        has_person = False
        for box in results[0].boxes:
            if int(box.cls[0]) == 0: 
                has_person = True
                break
                
        if has_person:
            face_evidence = frame.copy()
            
            gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
            faces = face_cascade.detectMultiScale(gray, 1.3, 5)
            if len(faces) > 0:
                (x, y, w, h) = faces[0]
                draw_box = (x, y, w, h)

            if draw_box is not None:
                x, y, w, h = draw_box
                cv2.rectangle(frame, (x, y), (x+w, y+h), draw_color, 3)
                (tw, th), _ = cv2.getTextSize(draw_label, cv2.FONT_HERSHEY_SIMPLEX, 0.7, 2)
                cv2.rectangle(frame, (x, y - th - 10), (x + tw + 10, y), draw_color, -1)
                cv2.putText(frame, draw_label, (x + 5, y - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
                
            rem_time = max(0, int(scan_end_time - time.time()))
            timer_text = f"Timeout: {rem_time}s"
            cv2.putText(frame, timer_text, (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 1, draw_color, 3)
        else:
            face_evidence = None
            draw_box = None
            cv2.putText(frame, "Scan face in camera...", (20, 40), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 165, 255), 3)

        ret, buffer = cv2.imencode('.jpg', frame)
        yield (b'--frame\r\n' b'Content-Type: image/jpeg\r\n\r\n' + buffer.tobytes() + b'\r\n')

@app.route('/video_feed')
def video_feed():
    return Response(generate_frames(), mimetype='multipart/x-mixed-replace; boundary=frame')

if __name__ == '__main__':
    print("🚀 TRẠM AI CHUÔNG CỬA ĐANG CHẠY ")
    app.run(host='0.0.0.0', port=5000, debug=False, use_reloader=False)