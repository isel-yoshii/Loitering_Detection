using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections; 

public class UIManager : MonoBehaviour
{
    public YoloReceiver receiver;
    public YodomiController yodomiController; 

    [Header("UIパネル")]
    public GameObject menuPanel;
    public GameObject settingsPanel; 
    public GameObject autoCalibrationPanel;   // 自動マーカー用のパネル（1秒で自動消去）

    [Header("ステータス表示")]
    public TMP_Text statusText;

    [Header("プロジェクター側の設定 (手動用)")]
    public GameObject[] projectorGuides; // 手動用の4つの的（個別オブジェクト）
    public GameObject waterFlowObject; 

    void Update()
    {
        if (receiver != null && statusText != null)
        {
            statusText.text = receiver.isConnected ? "Python接続: OK (動作中)" : "Python接続: 切断 または 待機中";
            statusText.color = receiver.isConnected ? Color.green : Color.red;
        }
    }

    // ===============================================
    // 自動キャリブレーション（1秒で消える＋枠が残る）
    // ===============================================
    public void StartAutoCalibration()
    {
        StartCoroutine(AutoCalibrationRoutine());
    }

    private IEnumerator AutoCalibrationRoutine()
    {
        // ★追加: 新しく設定をやり直すため、前回残っていた枠を一旦消す
        UpdateGuide(-1);

        if (autoCalibrationPanel != null)
        {
            autoCalibrationPanel.SetActive(true);
            Debug.Log("自動キャリブレーション開始：マーカーを1秒間表示します");
            
            yield return new WaitForSeconds(1.0f);
            
            autoCalibrationPanel.SetActive(false);
            Debug.Log("自動キャリブレーション完了");
        }
    }

    // ===============================================
    // 手動キャリブレーション（以前の動いていた挙動）
    // ===============================================
    public void OpenManualCalibration()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        UpdateGuide(0); // 最初の的を表示
    }
    
    public void CloseManualCalibration()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        UpdateGuide(-1); // すべての的を非表示にする
    }
    
    public void UpdateGuide(int step)
    {
        for (int i = 0; i < projectorGuides.Length; i++) 
        {
            if (projectorGuides[i] != null)
            {
                projectorGuides[i].SetActive(i == step);
            }
        }
    }

    // ===============================================
    // 設定・その他の機能
    // ===============================================
    public void OpenSettings()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }
    
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }
    
    public void RotateWaterFlow()
    {
        if (waterFlowObject != null) waterFlowObject.transform.Rotate(0, 90, 0); 
    }
    
    public void ChangeYodomiMode(int modeIndex)
    {
        if (yodomiController != null) yodomiController.SetMode(modeIndex);
    }
    
    public void QuitApp()
    {
        Application.Quit(); 
    }
}