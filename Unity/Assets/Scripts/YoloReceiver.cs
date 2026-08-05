using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class YoloReceiver : MonoBehaviour
{
    [Header("サーバー設定")]
    public string serverUrl = "http://localhost:5050";

    [Header("現在の状態（Pythonから受信）")]
    public int isStaying = 0;
    public float posX = 0.5f;
    public float posY = 0.5f;

    [Header("カメラ映像（UI表示用）")]
    public Texture2D cameraTexture;

    [Header("通信状態")]
    public bool isConnected = false;
    private float lastReceiveTime = 0f;

    [System.Serializable]
    public class YoloResponse
    {
        public int is_staying;
        public float pos_x;
        public float pos_y;
    }

    // JSON送信用のクラス
    [System.Serializable]
    public class ConfigData
    {
        public List<float[]> points;
    }

    void Start()
    {
        StartCoroutine(CheckStatusLoop());
        StartCoroutine(GetImageLoop());
    }

    void Update()
    {
        // 最後に通信を受信してから2秒以内なら「接続中」と判定
        isConnected = (Time.time - lastReceiveTime) < 2.0f;
    }

    // 1. 状態の受信（今まで通り）
    IEnumerator CheckStatusLoop()
    {
        while (true)
        {
            using (UnityWebRequest uwr = UnityWebRequest.Get(serverUrl + "/status"))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    lastReceiveTime = Time.time;
                    
                    YoloResponse data = JsonUtility.FromJson<YoloResponse>(uwr.downloadHandler.text);
                    if (data != null)
                    {
                        isStaying = data.is_staying;
                        posX = data.pos_x;
                        posY = data.pos_y;
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    // 2. ★新規：カメラ映像の受信
    IEnumerator GetImageLoop()
    {
        while (true)
        {
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(serverUrl + "/image"))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    // 古いテクスチャのメモリ解放（重要）
                    if (cameraTexture != null) Destroy(cameraTexture);
                    cameraTexture = DownloadHandlerTexture.GetContent(uwr);
                }
            }
            yield return new WaitForSeconds(0.1f); // 10FPS程度で更新
        }
    }

    // 3. ★新規：キャリブレーションの4点を送信する
    public void SendCalibrationData(List<Vector2> clickPoints)
    {
        if (clickPoints.Count < 4) return;
        StartCoroutine(PostConfig(clickPoints));
    }

    IEnumerator PostConfig(List<Vector2> clickPoints)
    {
        // UnityのJsonUtilityを使わず、Pythonが完璧に読める形式のJSONを直接組み立てる
        string json = "{\"points\":[" +
            $"[{clickPoints[0].x},{clickPoints[0].y}]," +
            $"[{clickPoints[1].x},{clickPoints[1].y}]," +
            $"[{clickPoints[2].x},{clickPoints[2].y}]," +
            $"[{clickPoints[3].x},{clickPoints[3].y}]" +
            "]}";

        Debug.Log("送信するJSON: " + json); // コンソールで中身を確認できます

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest uwr = new UnityWebRequest(serverUrl + "/config", "POST"))
        {
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");

            yield return uwr.SendWebRequest();

            if (uwr.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Pythonにキャリブレーション設定を送信しました！");
            }
            else
            {
                Debug.LogError("送信エラー: " + uwr.error);
            }
        }
    }
}