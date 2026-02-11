using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 長押し中にカメラを移動させる。
/// 移動方向 = 指定 Transform → 長押し位置 のベクトル。
/// 長押し位置が画面端から一定距離以上離れている場合はカメラ移動を無効にする。
/// Line システムには一切依存しない。
/// </summary>
public class LineHeadCameraFollow : MonoBehaviour
{
    [Header("方向の基準点")]
    [SerializeField, Tooltip("カメラ移動方向の起点となる Transform（例：赤丸の位置など）")]
    private Transform directionOrigin;

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
            Debug.LogError("LineHeadCameraFollow: Camera.main が見つかりません。");
            enabled = false;
            return;
        }
    }

    void LateUpdate()
    {
        if (mainCam == null || directionOrigin == null) return;

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

        // 長押し位置をワールド座標に変換
        Vector2 screenPos = pointer.position.ReadValue();
        Vector3 pressWorld = mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, mainCam.nearClipPlane));
        pressWorld.z = 0f;

        // ビューポート座標で画面端チェック
        Vector3 pressVp = mainCam.ScreenToViewportPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        bool nearEdge = pressVp.x < edgeActivationMargin
                     || pressVp.x > 1f - edgeActivationMargin
                     || pressVp.y < edgeActivationMargin
                     || pressVp.y > 1f - edgeActivationMargin;

        if (!nearEdge)
        {
            LogThrottled($"LineHeadCam: not near edge vp={pressVp}");
            return;
        }

        float heldSeconds = Time.unscaledTime - holdStartTime;
        float allowedMove = GetAllowedMoveDistance(heldSeconds);
        if (allowedMove <= 0f)
        {
            LogThrottled($"LineHeadCam: hold={heldSeconds:F2}s not enough");
            return;
        }

        // 方向 = directionOrigin → 長押し位置
        Vector3 originWorld = directionOrigin.position;
        originWorld.z = 0f;
        Vector3 dir = (pressWorld - originWorld);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector3 camPos = mainCam.transform.position;
        float movedSoFar = Vector2.Distance(new Vector2(camPos.x, camPos.y), new Vector2(holdStartCamPos.x, holdStartCamPos.y));
        float remaining = allowedMove - movedSoFar;
        if (remaining <= 0f)
        {
            LogThrottled($"LineHeadCam: move limit reached {allowedMove:F2}");
            return;
        }

        float step = cameraMoveSpeed * Time.deltaTime;
        if (step > remaining) step = remaining;

        // カメラ移動（Z は維持）
        float z = camPos.z;
        camPos += dir * step;
        camPos.z = z;
        mainCam.transform.position = camPos;

        LogThrottled($"LineHeadCam: moving dir={dir:F2} vp={pressVp:F2} camPos={camPos:F2} hold={heldSeconds:F2} allow={allowedMove:F2}");
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
