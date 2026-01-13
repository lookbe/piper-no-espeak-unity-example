using System.Collections;
using UMI;
using UnityEngine;

public class BasicMobileInput : BasicPiper
{
    const int MAX_LINES_COUNT = 3;
    const int MAX_LINE_CHARACTERS_COUNT = 33;
    const float CONTAINER_BOTTOM_OFFSET = 160f;
    const float MIN_INPUT_HEIGHT = 144f;
    const float LINE_HEIGHT = 48f;

    public RectTransform canvasRect;
    public RectTransform inputRect;
    public RectTransform chatRect;
    private MobileInputField multiInput;

    float currentHeight = 0;
    bool keyboardVisible = false;
    bool isWaitingForExit = false;

    void Awake()
    {
        MobileInput.Init();
        MobileInput.OnKeyboardAction += OnKeyboardAction;

        // copy initial input to native input
        multiInput = chatInputField.gameObject.AddComponent<MobileInputField>();
        multiInput.IsManualHideControl = true;
        multiInput.Text = chatInputField.text;
    }

    void OnDestroy()
    {
        MobileInput.OnKeyboardAction -= OnKeyboardAction;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (keyboardVisible)
            {
                OnKeyboardAction(false, 0);
                multiInput.Hide();
            }
            else
            {
                if (isWaitingForExit)
                {
                    Application.Quit();
                }
                else
                {
                    StartCoroutine(DoubleTapSequence());
                }
            }
        }
    }

    void OnKeyboardAction(bool isShow, int height)
    {
        keyboardVisible = isShow;

        var ratio = (float)Screen.height / canvasRect.sizeDelta.y / MobileInput.GetScreenScale();
        var keyboardHeight = height / ratio;

#if UNITY_IOS
        var scale = MobileInput.GetScreenScale();
        if (scale >= 3f) 
        {
            keyboardHeight *= 2.8f / scale;
        }
#endif

        MoveInputBox(keyboardHeight);
    }

    void MoveInputBox(float height)
    {
        currentHeight = height;
        inputRect.offsetMax = new Vector2(0f, currentHeight + inputRect.sizeDelta.y);
        inputRect.offsetMin = new Vector2(0f, currentHeight);
        var offset = currentHeight + inputRect.sizeDelta.y;
        chatRect.offsetMin = new Vector2(chatRect.offsetMin.x, offset);
    }

    protected override void ClearInput()
    {
        base.ClearInput();
        multiInput.Text = "";
    }

    IEnumerator DoubleTapSequence()
    {
        isWaitingForExit = true;
        ShowNativeToast("Press back again to exit");
        yield return new WaitForSeconds(2.0f);
        isWaitingForExit = false;
    }

    private void ShowNativeToast(string message)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");

        currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
            AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>(
                "makeText", currentActivity, message, 0); // 0 is LENGTH_SHORT
            toastObject.Call("show");
        }));
#else
        Debug.Log("Toast: " + message);
#endif
    }
}
