using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 長押し中にカメラを移動させる（方向は画面の固定位置基準）。
/// 方向 = 指定ビューポート位置 → 長押し位置 のベクトル。
/// </summary>
public class LineHeadCameraFollowViewportOrigin : MonoBehaviour
{
    [Header("方向の基準点（画面固定）")]
    [SerializeField, Tooltip("ビューポート座標での基準点（0-1）。例: 0.5,0.5 は画面中央")]
    private Vector2 viewportOrigin = new Vector2(0.5f, 0.5f);

    [Header("カメラ移動設定")]
    [SerializeField, Tooltip("カメラの移動速度（ワールド単位/秒）")]
    private float cameraMoveSpeed = 3f;

    [SerializeField, Tooltip("移動が有効になる長押し時間（秒）")]
    private float holdSecondsStep = 3f;

    [SerializeField, Tooltip("holdSecondsStep ごとに許可される移動量")]
    private float moveLimitStep = 10f;

    [Header("画面端の判定")]
    [SerializeField, Range(0f, 0.5f), Tooltip("画面端からの距離（ビューポート単位）。長押し位置がこの範囲内にあるときだけカメラが動く")]
    private float edgeActivationMargin = 0.2f;

    [Header("デバッグ")]
    [SerializeField] private bool enableLog = false;

    private Camera mainCam;
    private float nextLogTime;
    private bool wasPressed;
    private float holdStartTime;
    private Vector3 holdStartCamPos;

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("LineHeadCameraFollowViewportOrigin: Camera.main が見つかりません。");
            enabled = false;
            return;
        }
    }

    void LateUpdate()
    {
        if (mainCam == null) return;

        var pointer = Pointer.current;
        if (pointer == null) return;

        bool isPressed = pointer.press.isPressed;
        if (!isPressed)
        {
            wasPressed = false;
            return;
        }

        if (!wasPressed)
        {
            wasPressed = true;
            holdStartTime = Time.unscaledTime;
            holdStartCamPos = mainCam.transform.position;
        }

        Vector2 screenPos = pointer.position.ReadValue();
        Vector3 pressVp = mainCam.ScreenToViewportPoint(new Vector3(screenPos.x, screenPos.y, 0f));

        bool nearEdge = pressVp.x < edgeActivationMargin
                     || pressVp.x > 1f - edgeActivationMargin
                     || pressVp.y < edgeActivationMargin
                     || pressVp.y > 1f - edgeActivationMargin;

        if (!nearEdge)
        {
            LogThrottled($"ViewportOriginCam: not near edge vp={pressVp}");
            return;
        }

        float heldSeconds = Time.unscaledTime - holdStartTime;
        float allowedMove = GetAllowedMoveDistance(heldSeconds);
        if (allowedMove <= 0f)
        {
            LogThrottled($"ViewportOriginCam: hold={heldSeconds:F2}s not enough");
            return;
        }

        Vector2 dirVp = new Vector2(pressVp.x - viewportOrigin.x, pressVp.y - viewportOrigin.y);
        if (dirVp.sqrMagnitude < 0.0001f) return;
        dirVp.Normalize();

        Vector3 worldDir = mainCam.transform.right * dirVp.x + mainCam.transform.up * dirVp.y;
        worldDir.z = 0f;
        if (worldDir.sqrMagnitude < 0.0001f) return;
        worldDir.Normalize();

        Vector3 camPos = mainCam.transform.position;
        float movedSoFar = Vector2.Distance(new Vector2(camPos.x, camPos.y), new Vector2(holdStartCamPos.x, holdStartCamPos.y));
        float remaining = allowedMove - movedSoFar;
        if (remaining <= 0f)
        {
            LogThrottled($"ViewportOriginCam: move limit reached {allowedMove:F2}");
            return;
        }

        float step = cameraMoveSpeed * Time.deltaTime;
        if (step > remaining) step = remaining;

        float z = camPos.z;
        camPos += worldDir * step;
        camPos.z = z;
        mainCam.transform.position = camPos;

        LogThrottled($"ViewportOriginCam: moving dir={worldDir:F2} vp={pressVp:F2} camPos={camPos:F2} hold={heldSeconds:F2} allow={allowedMove:F2}");
    }

    private float GetAllowedMoveDistance(float heldSeconds)
    {
        if (holdSecondsStep <= 0f || moveLimitStep <= 0f) return 0f;
        if (heldSeconds < holdSecondsStep) return 0f;

        int tiers = Mathf.FloorToInt(heldSeconds / holdSecondsStep);
        return tiers * moveLimitStep;
    }

    private void LogThrottled(string message)
    {
        if (!enableLog) return;
        if (Time.unscaledTime < nextLogTime) return;
        nextLogTime = Time.unscaledTime + 0.2f;
        Debug.Log(message);
    }
}
