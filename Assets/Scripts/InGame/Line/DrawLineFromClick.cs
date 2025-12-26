using System;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using UnityEngine.InputSystem;

public class DrawLineFromClick : MonoBehaviour
{
    [Header("伸び方の調整")]
    [SerializeField, Tooltip("押している間に1秒あたり増えるワールド単位の長さ")]
    private float extendSpeedPerSecond = 2f;

    [Header("生成するLineRenderer（Prefab推奨）")]
    [SerializeField] private LineRenderer linePrefab;

    [Header("カウントダウン表示側")]
    [SerializeField] private CountdownPresenter countdownPresenter;

    [Header("影ライン設定")]
    [SerializeField] private bool useShadow = true;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private float shadowWidthMultiplier = 1.25f;

    [SerializeField] private Vector3 shadowOffsetWorld = new Vector3(0.05f, -0.05f, 0f);

    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int mainSortingOrder = 0;

    [Header("方向が取れない時のデフォルト方向")]
    [SerializeField] private Vector2 defaultDirection = Vector2.right;

    [Header("開始位置ガイド（赤丸で上書き）")]
    [SerializeField] private MonoBehaviour startPointOverrideSource; // IStartPointOverride2D を実装したコンポーネント（例: StickStartGuide2D）
    private IStartPointOverride2D startPointOverride;

    [Header("生成ラインのタグ設定")]
    [SerializeField, Tooltip("生成したLineRendererに自動で付与するタグ。空なら何もしない")] private string lineTag = "";

    private LineRenderer currentLine;
    private LineRenderer currentShadowLine;

    private Vector3 startPos;
    private Vector3 currentEndPos;

    private float currentLength;
    private Vector3 lastDir; // 正規化方向（方向だけマウスに追従）
    private bool isDrawing;
    private bool hasDrawnOnce;
    private bool canInput;
    private bool hasForcedStart;
    private Vector3 forcedStartPos;

    private readonly Subject<Vector3> onPressDown = new Subject<Vector3>();
    private readonly Subject<(LineRenderer line, LineRenderer shadow, Vector3 start, Vector3 end, Vector3 shadowOffsetWorld)>
        onPressUp = new Subject<(LineRenderer, LineRenderer, Vector3, Vector3, Vector3)>();

    public IObservable<Vector3> OnPressDownAsObservable => onPressDown;

    public IObservable<(LineRenderer line, LineRenderer shadow, Vector3 start, Vector3 end, Vector3 shadowOffsetWorld)> OnPressUpAsObservable
        => onPressUp;

    void Start()
    {
        if (linePrefab == null)
        {
            Debug.LogError("linePrefab が未設定です。LineRenderer付きPrefabをアサインしてね。");
            return;
        }
        if (countdownPresenter == null)
        {
            Debug.LogError("countdownPresenter が未設定です。CountdownPresenter をアサインしてね。");
            return;
        }

        bool isScoreAttack = ScoreAttackManager.Instance != null && ScoreAttackManager.Instance.IsRunning.Value;

        canInput = false;

        // 開始位置上書きの参照を解決
        if (startPointOverrideSource != null)
        {
            startPointOverride = startPointOverrideSource as IStartPointOverride2D;
            if (startPointOverride == null)
            {
                Debug.LogError("startPointOverrideSource は IStartPointOverride2D を実装していません。");
            }
        }

        countdownPresenter.OnCountdownFinished
            .Take(1)
            .Subscribe(_ => canInput = true)
            .AddTo(this);

        var pointerStream = this.UpdateAsObservable()
            .Select(_ => Pointer.current)
            .Where(p => p != null);

        var pressDown = pointerStream
            .Where(_ => canInput)
            .Where(_ => !hasDrawnOnce)
            .Where(p => p.press.wasPressedThisFrame)
            .Select(p => GetPointerWorldPos(p))
            .Share();

        var pressUp = pointerStream
            .Where(_ => isDrawing)
            .Where(p => p.press.wasReleasedThisFrame)
            .Select(_ => (line: currentLine, shadow: currentShadowLine, start: startPos, end: currentEndPos, shadowOffsetWorld: shadowOffsetWorld))
            .Share();

        pressDown
            .Where(pos =>
            {
                // 赤丸以外の位置からは線を引けない
                if (startPointOverride != null)
                {
                    return startPointOverride.TryOverrideStartPoint(pos, out _);
                }
                return true; // startPointOverrideがない場合は制限なし
            })
            .Subscribe(pos =>
            {
                hasDrawnOnce = true;
                isDrawing = true;

                // 赤丸内で押された場合は中心に開始点を固定
                if (startPointOverride != null && startPointOverride.TryOverrideStartPoint(pos, out var overrideStart))
                {
                    hasForcedStart = true;
                    forcedStartPos = overrideStart;
                }

                if (hasForcedStart)
                {
                    startPos = forcedStartPos;
                    hasForcedStart = false;
                }
                else
                {
                    startPos = pos;
                }

                currentEndPos = startPos;

                currentLength = 0f;
                lastDir = ((Vector3)defaultDirection).sqrMagnitude > 0.0001f ? ((Vector3)defaultDirection).normalized : Vector3.right;

                currentLine = Instantiate(linePrefab, transform);
                currentLine.name = "Line";
                currentLine.useWorldSpace = true;
                currentLine.positionCount = 2;
                currentLine.SetPosition(0, startPos);
                currentLine.SetPosition(1, currentEndPos);
                currentLine.sortingLayerName = sortingLayerName;
                currentLine.sortingOrder = mainSortingOrder;
                ApplyTagIfNeeded(currentLine);

                currentShadowLine = null;
                if (useShadow)
                {
                    currentShadowLine = Instantiate(linePrefab, transform);
                    currentShadowLine.name = "Line_Shadow";
                    currentShadowLine.useWorldSpace = true;
                    currentShadowLine.positionCount = 2;

                    currentShadowLine.startColor = shadowColor;
                    currentShadowLine.endColor = shadowColor;

                    currentShadowLine.startWidth = currentLine.startWidth * shadowWidthMultiplier;
                    currentShadowLine.endWidth = currentLine.endWidth * shadowWidthMultiplier;

                    currentShadowLine.sortingLayerName = sortingLayerName;
                    currentShadowLine.sortingOrder = mainSortingOrder - 1;

                    currentShadowLine.SetPosition(0, startPos + shadowOffsetWorld);
                    currentShadowLine.SetPosition(1, currentEndPos + shadowOffsetWorld);

                    ApplyTagIfNeeded(currentShadowLine);
                }

                onPressDown.OnNext(startPos);
            })
            .AddTo(this);

        // ✅ 押している間：長さは「増える一方」。マウスは「方向」だけ決める。
        this.UpdateAsObservable()
            .Where(_ => isDrawing)
            .Subscribe(_ =>
            {
                var p = Pointer.current;
                if (p == null || currentLine == null) return;

                Vector3 pointerPos = GetPointerWorldPos(p);
                Vector3 dir = pointerPos - startPos;

                if (dir.sqrMagnitude > 0.000001f)
                    lastDir = dir.normalized;

                currentLength += extendSpeedPerSecond * Time.deltaTime;
                currentEndPos = startPos + lastDir * currentLength;

                currentLine.SetPosition(1, currentEndPos);
                if (currentShadowLine != null)
                    currentShadowLine.SetPosition(1, currentEndPos + shadowOffsetWorld);
            })
            .AddTo(this);

        pressUp
            .Subscribe(tuple =>
            {
                isDrawing = false;

                if (tuple.line != null)
                {
                    tuple.line.SetPosition(0, tuple.start);
                    tuple.line.SetPosition(1, tuple.end);
                }

                if (tuple.shadow != null)
                {
                    tuple.shadow.SetPosition(0, tuple.start + shadowOffsetWorld);
                    tuple.shadow.SetPosition(1, tuple.end + shadowOffsetWorld);
                }

                onPressUp.OnNext(tuple);
            })
            .AddTo(this);
    }

    private Vector3 GetPointerWorldPos(Pointer p)
    {
        Vector2 screen = p.position.ReadValue();
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Camera.main が見つかりません。MainCameraタグを確認してね。");
            return Vector3.zero;
        }

        Vector3 pos = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, cam.nearClipPlane));
        pos.z = 0f;
        return pos;
    }

    public void ForceNextStartPoint(Vector3 worldPos)
    {
        worldPos.z = 0f;
        hasForcedStart = true;
        forcedStartPos = worldPos;
    }

    private void ApplyTagIfNeeded(LineRenderer lr)
    {
        if (lr == null) return;
        if (string.IsNullOrEmpty(lineTag)) return;
        lr.gameObject.tag = lineTag;
    }

    void OnDestroy()
    {
        onPressDown.OnCompleted();
        onPressDown.Dispose();

        onPressUp.OnCompleted();
        onPressUp.Dispose();
    }
}
