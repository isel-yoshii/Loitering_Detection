using UnityEngine;

public class DisplayActivator : MonoBehaviour
{
    void Start()
    {
        // 1. まずはDisplay 2（設定画面）を安全にアクティブにする
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
        
    }
}