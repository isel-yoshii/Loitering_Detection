from flask import Flask, jsonify, request
import cv2
import threading
from calibration import CalibrationManager
from tracker import camera_loop

app = Flask(__name__)

# --- 全体で共有するデータと管理クラス ---
calib_manager = CalibrationManager()
shared_state = {
    "latest_frame": None,
    "status": {
        "is_staying": 0,
        "stay_time": 0.0,
        "pos_x": 0.5,
        "pos_y": 0.5
    }
}

# --- エンドポイント ---

@app.route('/image')
def get_image():
    """UnityへYOLOの映像をJPEGで配信"""
    frame = shared_state["latest_frame"]
    if frame is None:
        return "No image", 404
    _, buffer = cv2.imencode('.jpg', frame)
    return buffer.tobytes(), 200, {'Content-Type': 'image/jpeg'}

@app.route('/status')
def get_status():
    """Unityへ現在の状態と座標を配信"""
    return jsonify(shared_state["status"])

@app.route('/config', methods=['POST'])
def receive_config():
    """Unityからキャリブレーションの4点を受信"""
    data = request.json
    print(f"Unityから設定を受信しました: {data}")
    
    if data is not None and "points" in data:
        success = calib_manager.update_points(data["points"])
        if success:
            print("ホモグラフィ行列と判定エリアを更新しました！")
            return jsonify({"message": "キャリブレーションを適用しました"})
            
    return jsonify({"error": "不正なデータ形式です"}), 400

# --- 実行 ---
if __name__ == '__main__':
    # YOLOのトラッキング処理を裏スレッドで開始
    threading.Thread(target=camera_loop, args=(shared_state, calib_manager), daemon=True).start()
    
    # Flaskサーバーを起動
    app.run(host='127.0.0.1', port=5050)