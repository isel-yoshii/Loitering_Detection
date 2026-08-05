import cv2
import numpy as np

# マーカーの種類を設定（4x4マスのシンプルなもの）
try:
    aruco_dict = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)
except AttributeError:
    aruco_dict = cv2.aruco.Dictionary_get(cv2.aruco.DICT_4X4_50)

# ID 0〜3 の4つのマーカー画像を生成して保存
for i in range(4):
    # 200x200ピクセルのマーカー画像を生成
    try:
        img = cv2.aruco.generateImageMarker(aruco_dict, i, 200)
    except AttributeError:
        img = cv2.aruco.drawMarker(aruco_dict, i, 200)
    
    # マーカーの周りに「白い余白」をつける（※プロジェクター投影時に必須！）
    img_with_border = cv2.copyMakeBorder(img, 20, 20, 20, 20, cv2.BORDER_CONSTANT, value=[255, 255, 255])
    
    # 保存
    cv2.imwrite(f"marker_ID{i}.png", img_with_border)
    print(f"marker_ID{i}.png を作成しました！")