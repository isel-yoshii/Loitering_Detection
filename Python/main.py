from flask import Flask, jsonify, request
import cv2
import threading
import time
from ultralytics import YOLO

app = Flask(__name__)

# 最新の映像と判定結果を保持するグローバル変数
latest_frame = None
current_status = {"is_staying": 0, "stay_time": 0.0}

# --- 設定値 ---
WARNING_THRESHOLD = 5.0  # 警告状態になるまでの秒数
RESET_THRESHOLD = 1.0    # 何秒間カメラから消えたらリセットするか

# --- カメラとYOLOの処理（別スレッドで裏側で常に回す） ---
def camera_loop():
    global latest_frame, current_status
    cap = cv2.VideoCapture(0) # カメラ起動

    print("YOLOモデルを読み込んでいます...")
    model = YOLO('yolo26n.pt') 
    print("読み込み完了！カメラ処理を開始します。")
    
    object_timers = {}
    
    while True:
            ret, frame = cap.read()
            if ret:
                current_time = time.time()
                
                # --- 1. YOLOトラッキングの実行 ---
                # persist=True で前フレームのIDを引き継ぎます
                results = model.track(frame, persist=True, verbose=False)
                result = results[0]
                
                # ★Unityに送るための「画面内の最大滞在時間」と「警告フラグ」
                max_stay_time = 0.0
                is_anyone_warning = 0

                if result.boxes is not None and result.boxes.id is not None:
                    boxes = result.boxes.xyxy.cpu().numpy().astype(int)
                    track_ids = result.boxes.id.cpu().numpy().astype(int)
                    class_ids = result.boxes.cls.cpu().numpy().astype(int)

                    for box, track_id, class_id in zip(boxes, track_ids, class_ids):
                        x1, y1, x2, y2 = box
                        class_name = model.names[class_id]

                        # --- 2. 滞在時間パラメータの更新 ---
                        if track_id not in object_timers:
                            object_timers[track_id] = {
                                'first_seen': current_time,
                                'last_seen': current_time,
                                'is_warning': False
                            }
                        else:
                            object_timers[track_id]['last_seen'] = current_time

                        elapsed_time = current_time - object_timers[track_id]['first_seen']

                        if elapsed_time >= WARNING_THRESHOLD:
                            object_timers[track_id]['is_warning'] = True

                        # --- 3. 描画の変更 ---
                        is_warning = object_timers[track_id]['is_warning']
                        
                        if is_warning:
                            color = (0, 0, 255) # 警告：赤
                            thickness = 4
                            label = f"ISUWARI ID:{track_id} {class_name} ({int(elapsed_time)}s)"
                            is_anyone_warning = 1 # 誰か一人でも警告状態ならフラグを立てる
                        else:
                            color = (0, 255, 0) if class_id == 0 else (0, 165, 255) # 人:緑 モノ:オレンジ
                            thickness = 2
                            label = f"ID:{track_id} {class_name} ({int(elapsed_time)}s)"

                        cv2.rectangle(frame, (x1, y1), (x2, y2), color, thickness)
                        cv2.putText(frame, label, (x1, max(20, y1 - 10)), 
                                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, color, thickness)
                        
                        # 画面内で一番長く映っている時間を記録
                        if elapsed_time > max_stay_time:
                            max_stay_time = elapsed_time

                # --- 4. 退出処理（瞬き対策） ---
                for tid in list(object_timers.keys()):
                    if current_time - object_timers[tid]['last_seen'] > RESET_THRESHOLD:
                        del object_timers[tid]

                # --- 5. Unityへ送信する変数を更新 ---
                current_status["is_staying"] = is_anyone_warning
                current_status["stay_time"] = round(max_stay_time, 1)

                latest_frame = frame
                
            time.sleep(0.03)

# --- エンドポイント①：映像の配信 ---
@app.route('/image')
def get_image():
    if latest_frame is None:
        return "No image", 404
    # 画像をJPEGに圧縮して送信
    _, buffer = cv2.imencode('.jpg', latest_frame)
    return buffer.tobytes(), 200, {'Content-Type': 'image/jpeg'}

# --- エンドポイント②：判定結果の配信 ---
@app.route('/status')
def get_status():
    # 辞書データを自動でJSON形式にして返す
    return jsonify(current_status)

# --- エンドポイント③：Unityからの設定受信 ---
@app.route('/config', methods=['POST'])
def receive_config():
    # Unityから送られてきたJSONデータを受け取る
    data = request.json
    print(f"Unityから設定を受信しました: {data}")
    # ＝＝＝ ここでYOLOの判定エリアなどの変数を更新する ＝＝＝
    return jsonify({"message": "設定を適用しました"})

if __name__ == '__main__':
    # カメラの処理を裏スレッドで開始
    threading.Thread(target=camera_loop, daemon=True).start()
    # サーバー起動
    app.run(host='127.0.0.1', port=5050)