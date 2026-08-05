using UnityEngine;
using UnityEngine.Networking;
using System.Collections;


public class SensorClient : MonoBehaviour
{
    [Header("映像表示用のUI")]
    public UnityEngine.UI.RawImage displayImage;

    // --- JSONのデータ構造と一致させるクラス ---
    [System.Serializable]
    public class SensorStatus
    {
        public int is_staying;
        public float stay_time;
    }

    [Header("現在の判定結果（Inspectorで確認可能）")]
    public SensorStatus currentStatus;

    void Start()
    {
        // 映像とデータを取得するループ処理をスタート
        StartCoroutine(GetImageRoutine());
        StartCoroutine(GetStatusRoutine());
    }

    [Header("比率調整用")]
    public UnityEngine.UI.AspectRatioFitter aspectFitter;

    // --- ① 映像を取得するコルーチン ---
    IEnumerator GetImageRoutine()
    {
        while (true)
        {
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture("http://127.0.0.1:5050/image"))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                    displayImage.texture = tex;

                    if (aspectFitter != null)
                    {
                        aspectFitter.aspectRatio = (float)tex.width / (float)tex.height;
                    }
                }
                else
                {
                    // ★追加：画像が取れなかった理由をコンソールに赤文字で表示する
                    Debug.LogError("画像受信エラー: " + uwr.error);
                }
            }
            // 映像は毎フレーム取得すると重いので、適度に間引く（0.1秒 = 10fps）
            yield return new WaitForSeconds(0.1f);
        }
    }

    // --- ② 判定結果を取得するコルーチン ---
    IEnumerator GetStatusRoutine()
    {
        while (true)
        {
            using (UnityWebRequest uwr = UnityWebRequest.Get("http://127.0.0.1:5050/status"))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    // JSON文字列をC#のクラスに変換
                    string json = uwr.downloadHandler.text;
                    currentStatus = JsonUtility.FromJson<SensorStatus>(json);
                    
                    // ※ここで currentStatus.is_staying を使って
                    // キャラクターを動かすなどの処理を呼ぶ
                }
                else
                {
                    // ★追加：データが取れなかった理由をコンソールに赤文字で表示する
                    Debug.LogError("データ受信エラー: " + uwr.error);
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    // --- ③ UnityからPythonへ設定を送る場合（参考） ---
    public void SendAreaConfig()
    {
        StartCoroutine(PostConfigRoutine());
    }

    IEnumerator PostConfigRoutine()
    {
        string jsonToSend = "{\"x_min\": 100, \"x_max\": 400}"; // 例
        using (UnityWebRequest uwr = new UnityWebRequest("http://127.0.0.1:5050/config", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonToSend);
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            yield return uwr.SendWebRequest();
        }
    }
}