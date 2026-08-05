using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CalibrationUI : MonoBehaviour, IPointerClickHandler
{
    [Header("通信スクリプト")]
    public YoloReceiver receiver;

    [Header("映像を表示するRawImage")]
    public RawImage displayImage;

    [Header("UIマネージャー")]
    public UIManager uiManager;

    // クリックした座標を保存するリスト
    private List<Vector2> clickedPoints = new List<Vector2>();

    void Update()
    {
        // YoloReceiverが受信した最新のカメラ映像をRawImageに貼る
        if (receiver != null && receiver.cameraTexture != null)
        {
            displayImage.texture = receiver.cameraTexture;

            // ★追加：映像の縦横ピクセル数から、自動でアスペクト比を計算して適用する
            AspectRatioFitter fitter = displayImage.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                float texWidth = receiver.cameraTexture.width;
                float texHeight = receiver.cameraTexture.height;
                
                // ゼロ割りエラーを防ぐための安全チェック
                if (texHeight > 0)
                {
                    fitter.aspectRatio = texWidth / texHeight;
                }
            }
        }
    }

    // UIのRawImageがクリックされた時に呼ばれる
    public void OnPointerClick(PointerEventData eventData)
    {
        if (receiver == null || receiver.cameraTexture == null) return;

        // 1. UI上のローカル座標を取得
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            displayImage.rectTransform, 
            eventData.position, 
            eventData.pressEventCamera, 
            out Vector2 localPoint);

        float rectWidth = displayImage.rectTransform.rect.width;
        float rectHeight = displayImage.rectTransform.rect.height;
        
        // 2. ★重要★ クリックした場所が、画像の「左から何％、上から何％」かを 0.0〜1.0 の割合で計算
        float normalizedX = (localPoint.x + (rectWidth / 2f)) / rectWidth;
        float normalizedY = ((rectHeight / 2f) - localPoint.y) / rectHeight; // Pythonに合わせてY軸を反転

        // 3. その割合を、実際のカメラの解像度（ピクセル）に掛け算して、正しい座標を算出！
        float pixelX = normalizedX * receiver.cameraTexture.width;
        float pixelY = normalizedY * receiver.cameraTexture.height;

        // リストに追加
        clickedPoints.Add(new Vector2(pixelX, pixelY));
        
        // プロジェクター側の的を「次の場所」に進める（UIManagerがある場合）
        if (uiManager != null) uiManager.UpdateGuide(clickedPoints.Count); 

        Debug.Log($"クリックしました: {clickedPoints.Count}点目 (画像上の実際の座標 X:{pixelX:F1}, Y:{pixelY:F1})");

        // 4点揃ったらPythonに送信してリセット
        if (clickedPoints.Count >= 4)
        {
            receiver.SendCalibrationData(clickedPoints);
            YodomiController yodomi = FindAnyObjectByType<YodomiController>();
            if (yodomi != null)
            {
                yodomi.CalculateAutoOffset(clickedPoints);
            }
            clickedPoints.Clear(); // 次のためにリセット
            Debug.Log("4点設定完了！Pythonに送信しました。");
            
            // 設定が終わったら設定画面を閉じてメニューに戻る
            if (uiManager != null) uiManager.CloseManualCalibration();
        }
    }
}