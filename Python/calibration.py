import cv2
import numpy as np

class CalibrationManager:
    def __init__(self):
        # 初期状態は画面全体をエリアとする（設定されるまでの仮）
        self.src_pts = np.float32([[0, 0], [640, 0], [640, 480], [0, 480]])
        # Unity側の四隅 (左下が0,0)
        self.dst_pts = np.float32([[0.0, 1.0], [1.0, 1.0], [1.0, 0.0], [0.0, 0.0]])
        self.matrix = cv2.getPerspectiveTransform(self.src_pts, self.dst_pts)
        self.is_calibrated = False # Unityから設定が来たかどうかのフラグ

    def update_points(self, points):
        """Unityから4点を受け取って行列を更新する"""
        if len(points) == 4:
            self.src_pts = np.float32(points)
            self.matrix = cv2.getPerspectiveTransform(self.src_pts, self.dst_pts)
            self.is_calibrated = True
            return True
        return False

    def update_auto_points(self, auto_points):
        """ArUcoマーカーから計算した4点で行列を更新する"""
        if len(auto_points) == 4:
            self.src_pts = np.float32(auto_points)
            self.matrix = cv2.getPerspectiveTransform(self.src_pts, self.dst_pts)
            self.is_calibrated = True
            print("★自動キャリブレーションにより座標が更新されました！")

    def is_inside_area(self, x, y):
        """足元座標 (x,y) が設定エリア内にあるか判定する"""
        if not self.is_calibrated:
            return True # 未設定時は画面のどこにいても有効とする
        
        # cv2.pointPolygonTest でポリゴン（4角形）の中に点があるか判定
        contour = self.src_pts.astype(np.int32)
        # 戻り値: +1(内側), 0(境界線上), -1(外側)
        result = cv2.pointPolygonTest(contour, (float(x), float(y)), False)
        return result >= 0

    def transform_to_unity(self, x, y):
        """カメラのピクセル座標をUnityの 0.0~1.0 に変換する"""
        pt = np.float32([[[x, y]]])
        transformed = cv2.perspectiveTransform(pt, self.matrix)
        ux = float(transformed[0][0][0])
        uy = float(transformed[0][0][1])
        # 0.0 ~ 1.0 の間にクリップして安全にする
        return max(0.0, min(1.0, ux)), max(0.0, min(1.0, uy))