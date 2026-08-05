import cv2
import time
import numpy as np
from ultralytics import YOLO

# --- 自動キャリブレーション（ArUco）の設定 ---
try:
    aruco_dict = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)
    parameters = cv2.aruco.DetectorParameters()
    detector = cv2.aruco.ArucoDetector(aruco_dict, parameters)
    def detect_markers(frame):
        return detector.detectMarkers(frame)
except AttributeError:
    # 古いバージョンのOpenCV用フォールバック
    aruco_dict = cv2.aruco.Dictionary_get(cv2.aruco.DICT_4X4_50)
    parameters = cv2.aruco.DetectorParameters_create()
    def detect_markers(frame):
        return cv2.aruco.detectMarkers(frame, aruco_dict, parameters=parameters)

# --- 設定値 ---
WARNING_THRESHOLD = 5.0
RESET_THRESHOLD = 1.0

def camera_loop(shared_state, calib_manager):
    """裏側でずっと回り続けるYOLOカメラループ"""
    cap = cv2.VideoCapture(0)
    
    print("YOLOモデルを読み込んでいます...")
    model = YOLO("yolo26n.pt") 
    print("読み込み完了！カメラ処理を開始します。")
    
    object_timers = {}

    while True:
        ret, frame = cap.read()
        if not ret:
            time.sleep(0.1)
            continue

        current_time = time.time()

        # ==============================================================
        # ★追加：マーカーの検知と自動キャリブレーション
        # ==============================================================
        corners, ids, rejected = detect_markers(frame)

        if ids is not None and len(ids) >= 4:
            # ID 0, 1, 2, 3 の4つが全て画面内に存在するか確認
            if all(i in ids for i in range(4)):
                auto_src_pts = np.zeros((4, 2), dtype=np.float32)
                for i in range(4):
                    idx = np.where(ids == i)[0][0]
                    marker_corners = corners[idx][0] 
                    
                    # マーカーの「中心」を計算
                    center = np.mean(marker_corners, axis=0)
                    
                    # それぞれの角の座標を取得
                    if i == 0:
                        corner = marker_corners[0] # 左上の角
                    elif i == 1:
                        corner = marker_corners[1] # 右上の角
                    elif i == 2:
                        corner = marker_corners[2] # 右下の角
                    elif i == 3:
                        corner = marker_corners[3] # 左下の角
                        
                    # ★魔法の計算：中心から角への距離を 1.35倍 に伸ばして、白いフチの外側を指定する！
                    vec = corner - center
                    expanded_corner = center + (vec * 1.35)
                    
                    auto_src_pts[i] = expanded_corner
                
                # CalibrationManager に送って座標を上書き更新！
                calib_manager.update_auto_points(auto_src_pts)
                
                # 成功したことを映像にも表示（緑色で大きく）
                cv2.putText(frame, "AUTO CALIB OK!", (50, 100), cv2.FONT_HERSHEY_SIMPLEX, 1.5, (0, 255, 0), 3)

        # ==============================================================
        # YOLOの推論と追跡
        # ==============================================================
        results = model.track(frame, persist=True, verbose=False)
        result = results[0]

        max_stay_time = 0.0
        is_anyone_warning = 0
        target_foot_x, target_foot_y = 0, 0

        # --- 映像上にキャリブレーションエリアの枠線を描画（黄色） ---
        if calib_manager.is_calibrated:
            pts = calib_manager.src_pts.astype(np.int32)
            cv2.polylines(frame, [pts], isClosed=True, color=(0, 255, 255), thickness=2)

        # 検出結果がある場合
        if result.boxes is not None and result.boxes.id is not None:
            boxes = result.boxes.xyxy.cpu().numpy().astype(int)
            track_ids = result.boxes.id.cpu().numpy().astype(int)
            class_ids = result.boxes.cls.cpu().numpy().astype(int)
            masks = result.masks.xy if result.masks is not None else [None] * len(boxes)

            for box, track_id, class_id, mask in zip(boxes, track_ids, class_ids, masks):
                x1, y1, x2, y2 = box
                
                if mask is not None and len(mask) > 0:
                    mask_pts = np.int32([mask])
                    cv2.polylines(frame, mask_pts, True, (0, 255, 0), 1)
                    mx, my, mw, mh = cv2.boundingRect(mask_pts[0])
                    foot_x = int(mx + mw / 2)
                    foot_y = int(my + mh * 0.85)
                else:
                    foot_x = int((x1 + x2) / 2)
                    foot_y = int(y2)
                
                cv2.circle(frame, (foot_x, foot_y), 6, (255, 255, 255), -1)

                # エリア内かどうかの判定
                in_area = calib_manager.is_inside_area(foot_x, foot_y)

                if in_area:
                    if track_id not in object_timers:
                        object_timers[track_id] = {'first_seen': current_time, 'last_seen': current_time}
                    else:
                        object_timers[track_id]['last_seen'] = current_time
                    
                    elapsed_time = current_time - object_timers[track_id]['first_seen']
                    
                    is_warning = elapsed_time >= WARNING_THRESHOLD
                    color = (0, 0, 255) if is_warning else (0, 255, 0)
                    
                    if is_warning:
                        is_anyone_warning = 1
                        if elapsed_time > max_stay_time:
                            max_stay_time = elapsed_time
                            target_foot_x, target_foot_y = foot_x, foot_y
                            cv2.circle(frame, (foot_x, foot_y), 10, (0, 0, 255), -1)
                            
                    cv2.putText(frame, f"ID:{track_id} ({int(elapsed_time)}s)", (x1, max(20, y1 - 10)), 
                                cv2.FONT_HERSHEY_SIMPLEX, 0.7, color, 2)
                else:
                    if track_id in object_timers:
                        del object_timers[track_id]
                    cv2.putText(frame, "OUT OF AREA", (x1, max(20, y1 - 10)), 
                                cv2.FONT_HERSHEY_SIMPLEX, 0.5, (100, 100, 100), 1)

        # 退室した人のタイマーを削除
        for tid in list(object_timers.keys()):
            if current_time - object_timers[tid]['last_seen'] > RESET_THRESHOLD:
                del object_timers[tid]

        # --- Unityへ送信する座標を計算 ---
        unity_x, unity_y = 0.5, 0.5
        if is_anyone_warning == 1:
            unity_x, unity_y = calib_manager.transform_to_unity(target_foot_x, target_foot_y)

        # 共有ステータスの更新
        shared_state["latest_frame"] = frame
        shared_state["status"]["is_staying"] = is_anyone_warning
        shared_state["status"]["stay_time"] = round(max_stay_time, 1)
        shared_state["status"]["pos_x"] = unity_x
        shared_state["status"]["pos_y"] = unity_y

        time.sleep(0.03)