using UnityEngine;
using System.Collections.Generic;

public class YodomiController : MonoBehaviour
{
    public enum YodomiMode { NormalTracking, StationaryVanish }

    public YoloReceiver receiver;
    public GameObject yodomiObject;
    public Camera yodomiCamera;

    [Header("現在のモード")]
    public YodomiMode currentMode = YodomiMode.NormalTracking;

    [Header("【モード1】追従の滑らかさ")]
    public float smoothTime = 0.2f; 

    [Header("【モード2】消えるまでの移動距離（メートル）")]
    public float vanishDistance = 1.0f; 

    [Header("見失ってから消えるまでの猶予時間（秒）")]
    public float hideDelay = 1.0f; 

    [Header("自動補正の強さ")]
    public float autoOffsetMultiplier = 0.3f; 

    private float autoDepthOffset = 0f;
    private float initialY;
    private Plane groundPlane; 
    private Vector3 currentVelocity = Vector3.zero;
    private float hideTimer = 0f;

    private Vector3 anchoredPos;
    private bool isWaitingForReset = false; 

    void Start()
    {
        if (yodomiObject != null)
        {
            initialY = yodomiObject.transform.position.y;
            groundPlane = new Plane(Vector3.up, new Vector3(0, initialY, 0));
        }
    }

    void Update()
    {
        if (receiver == null || yodomiObject == null || yodomiCamera == null) return;

        if (receiver.isStaying == 1)
        {
            hideTimer = 0f;
            if (isWaitingForReset) return;

            float px = Mathf.Clamp(receiver.posX, 0f, 1f);
            float py = Mathf.Clamp(receiver.posY + autoDepthOffset, 0f, 1f);

            Ray ray = yodomiCamera.ViewportPointToRay(new Vector3(px, py, 0));
            Vector3 targetPos = yodomiObject.transform.position; 

            if (groundPlane.Raycast(ray, out float enter)) targetPos = ray.GetPoint(enter);

            if (!yodomiObject.activeSelf) 
            {
                yodomiObject.SetActive(true);
                yodomiObject.transform.position = targetPos;
                anchoredPos = targetPos; 
                currentVelocity = Vector3.zero; 
            }
            else
            {
                if (currentMode == YodomiMode.NormalTracking)
                {
                    yodomiObject.transform.position = Vector3.SmoothDamp(
                        yodomiObject.transform.position, targetPos, ref currentVelocity, smoothTime);
                }
                else if (currentMode == YodomiMode.StationaryVanish)
                {
                    if (Vector3.Distance(anchoredPos, targetPos) > vanishDistance)
                    {
                        yodomiObject.SetActive(false);
                        isWaitingForReset = true; 
                    }
                    else
                    {
                        yodomiObject.transform.position = anchoredPos;
                    }
                }
            }
        }
        else
        {
            isWaitingForReset = false; 
            hideTimer += Time.deltaTime;
            if (hideTimer >= hideDelay && yodomiObject.activeSelf) yodomiObject.SetActive(false);
        }
    }

    public void CalculateAutoOffset(List<Vector2> points)
    {
        if (points.Count == 4)
        {
            float topWidth = Vector2.Distance(points[0], points[1]);
            float bottomWidth = Vector2.Distance(points[3], points[2]);
            if (bottomWidth > 0) autoDepthOffset = Mathf.Clamp((1f - (topWidth / bottomWidth)) * autoOffsetMultiplier, 0f, 0.5f);
        }
    }

    // ★UIのボタンから呼び出されるモード変更用の関数
    public void SetMode(int modeIndex)
    {
        if (modeIndex == 0) currentMode = YodomiMode.NormalTracking;
        else if (modeIndex == 1) currentMode = YodomiMode.StationaryVanish;
        Debug.Log("モードを変更しました: " + currentMode);
    }
}