using System;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using UnityEngine.SceneManagement;

/// <summary>
/// 棒のハマリ判定（要件準拠）
/// - 左クリックを離した瞬間から最大ハマリ率(maxRate)を追跡
/// - 長さ許容：±lengthTolerancePercent（例：±5%）
/// - ハマリ率：ターゲット線分に対する「線分の重なり率」
/// - maxRate >= (1 - lengthTolerancePercent) を満たした瞬間にクリア通知
/// - クリアしたら SnapCollider を有効化し、接触したらピタッと停止＆正解位置にスナップ
/// - クリアObservableを外部に公開
/// </summary>
public class StickFitJudgeRx : MonoBehaviour
{
    [Header("入力側")]
    [SerializeField] private DrawLineFromClick drawer;

    [Header("正解スロット（灰色形：SpriteRendererから自動計算）")]
    [SerializeField] private FitTargetSegment2D target;

    [Header("長さ許容（±5%）")]
    [SerializeField, Range(0f, 0.2f)] private float lengthTolerancePercent = 0.05f;

    [Header("姿勢（角度/ズレ）許容：※太さは無視するが、判定のために最小限の許容を置く")]
    [SerializeField, Range(0f, 30f)] private float angleToleranceDeg = 6f;
    [SerializeField] private float perpendicularToleranceMultiplier = 1.0f;

    [Header("失敗時：自動リスタート")]
    [SerializeField] private bool restartOnFail = true;
    [SerializeField] private float restartDelaySeconds = 0.5f;

    [Header("失敗判定（画面外に落ちた場合）")]
    [SerializeField] private float failYThreshold = -10f;

    [Header("失敗判定（棒が停止した場合）")]
    [SerializeField] private float stoppedVelocityThreshold = 0.1f;
    [SerializeField] private float stoppedTimeTolerance = 0.5f;
    [SerializeField] private float failureJudgmentDelaySeconds = 1f;

    public struct FitProgress
    {
        public LineRenderer line;
        public float currentRate;      // 現在のハマリ率（0～1）
        public float maxRate;          // 最大ハマリ率（0～1）
        public int currentPercent;     // 現在のハマリ率%
        public int maxPercent;         // 最大ハマリ率%
        public bool lengthOk;          // 長さが許容範囲内か
        public bool cleared;           // クリア通知済みか
        public bool failed;            // 失敗したか
    }

    private readonly Subject<FitProgress> onProgress = new Subject<FitProgress>();
    private readonly Subject<Unit> onCleared = new Subject<Unit>();

    public IObservable<FitProgress> OnProgressAsObservable => onProgress;
    public IObservable<Unit> OnClearedAsObservable => onCleared;

    private bool resolved;
    private bool enableFailureJudgment;

    // クリアは「通知」と「スナップ完了」は別扱い
    private bool clearSignaled;
    private bool snapped;

    private class TrackingState
    {
        public LineRenderer line;
        public LineRenderer shadow;
        public Rigidbody2D rb;
        public Collider2D col;

        public float maxRate;
        public bool lengthOk;
        public float stoppedTime;
    }

    private TrackingState trackingState;

    void Start()
    {
        if (drawer == null)
        {
            Debug.LogError("drawer が未設定です。DrawLineFromClick をアサインしてね。");
            return;
        }
        if (target == null)
        {
            Debug.LogError("target が未設定です。FitTargetSegment2D をアサインしてね。");
            return;
        }

        drawer.OnPressUpAsObservable
            .Where(_ => !resolved)
            .Subscribe(x => StartTracking(x.line, x.shadow))
            .AddTo(this);
    }

    private void StartTracking(LineRenderer line, LineRenderer shadow)
    {
        if (line == null) return;

        enableFailureJudgment = false;
        clearSignaled = false;
        snapped = false;

        // Rigidbody2D が付くまで待つ（LineDropOnRelease 側で付与される）
        Observable.EveryFixedUpdate()
            .Select(_ => line.GetComponent<Rigidbody2D>())
            .Where(rb => rb != null)
            .Take(1)
            .Subscribe(rb =>
            {
                trackingState = new TrackingState
                {
                    line = line,
                    shadow = shadow,
                    rb = rb,
                    col = line.GetComponent<Collider2D>(),
                    maxRate = 0f,
                    lengthOk = false,
                    stoppedTime = 0f
                };

                // 判定ループ（未解決の間だけ）
                Observable.EveryFixedUpdate()
                    .Where(_ => !resolved && trackingState != null)
                    .Subscribe(_ => TickJudge())
                    .AddTo(this);

                // 画面外で失敗
                Observable.EveryFixedUpdate()
                    .Where(_ => !resolved && trackingState?.line != null)
                    .Where(_ => trackingState.line.transform.position.y < failYThreshold)
                    .Take(1)
                    .Subscribe(_ => OnFailed())
                    .AddTo(this);

                // 失敗判定開始
                Observable.Timer(TimeSpan.FromSeconds(failureJudgmentDelaySeconds))
                    .Where(_ => !resolved && trackingState != null)
                    .Subscribe(_ => enableFailureJudgment = true)
                    .AddTo(this);

                // ✅ クリア後、SnapColliderに触れたらピタッと停止＆スナップ
                line.OnCollisionEnter2DAsObservable()
                    .Where(_ => clearSignaled && !snapped)
                    .Where(c => target != null && target.SnapCollider != null && c.collider == target.SnapCollider)
                    .Take(1)
                    .Subscribe(_ => SnapNow())
                    .AddTo(this);

                // 取りこぼし保険（Enableした瞬間に重なってた等）
                Observable.EveryFixedUpdate()
                    .Where(_ => clearSignaled && !snapped)
                    .Subscribe(_ => TrySnapIfTouching())
                    .AddTo(this);

                // ✅ クリア後に一定時間経っても停止しない場合は強制スナップ（フェイルセーフ）
                Observable.Timer(TimeSpan.FromSeconds(0.5f))
                    .Where(_ => clearSignaled && !snapped)
                    .Take(1)
                    .Subscribe(_ => ForceSnapOnClear())
                    .AddTo(this);
            })
            .AddTo(this);
    }

    private void TickJudge()
    {
        if (resolved || trackingState == null) return;
        if (target == null || !target.IsValid) return;

        var state = trackingState;
        if (state.line == null || state.rb == null) return;

        // 棒の端点（ワールド）
        GetStickWorldEndpoints(state.line, out Vector2 stickStart, out Vector2 stickEnd);

        Vector2 targetStart = target.Start;
        Vector2 targetEnd = target.End;

        float stickLen = Vector2.Distance(stickStart, stickEnd);
        float targetLen = Vector2.Distance(targetStart, targetEnd);

        if (stickLen <= 0.0001f || targetLen <= 0.0001f)
        {
            state.lengthOk = false;
            return;
        }

        // 長さ許容（±5%）
        float ratio = stickLen / targetLen;
        float minRatio = 1f - lengthTolerancePercent;
        float maxRatio = 1f + lengthTolerancePercent;
        state.lengthOk = (ratio >= minRatio && ratio <= maxRatio);

        // ハマリ率：線分の重なり率（長さOKの瞬間だけ max を更新）
        float fitRate = state.lengthOk ? CalcOverlapRate(stickStart, stickEnd, targetStart, targetEnd) : 0f;
        if (fitRate > state.maxRate) state.maxRate = fitRate;

        onProgress.OnNext(new FitProgress
        {
            line = state.line,
            currentRate = fitRate,
            maxRate = state.maxRate,
            currentPercent = ToPercent(fitRate),
            maxPercent = ToPercent(state.maxRate),
            lengthOk = state.lengthOk,
            cleared = false,
            failed = false
        });

        // ✅ クリア条件：長さOK かつ maxRate >= 0.95（±5%）
        float clearThreshold = 1f - lengthTolerancePercent;
        if (state.lengthOk && state.maxRate >= clearThreshold)
        {
            SignalClear();
            return;
        }

        // 失敗条件：停止
        if (enableFailureJudgment)
        {
            float v = state.rb.linearVelocity.magnitude;
            if (v < stoppedVelocityThreshold) state.stoppedTime += Time.fixedDeltaTime;
            else state.stoppedTime = 0f;

            if (state.stoppedTime >= stoppedTimeTolerance)
                OnFailed();
        }
    }

    /// <summary>
    /// クリアを「通知」する（この瞬間に CLEAR! にしたい）
    /// その後、SnapCollider を有効化して接触したら停止＆スナップ。
    /// </summary>
    private void SignalClear()
    {
        if (resolved) return;
        resolved = true;
        clearSignaled = true;

        if (target != null) target.EnableSnapCollider();

        // Presenter 側は cleared=true を受けて CLEAR! 表示に切り替える
        onProgress.OnNext(new FitProgress
        {
            line = trackingState != null ? trackingState.line : null,
            currentRate = 1f,
            maxRate = 1f,
            currentPercent = 100,
            maxPercent = 100,
            lengthOk = true,
            cleared = true,
            failed = false
        });

        onCleared.OnNext(Unit.Default);
    }

    private void TrySnapIfTouching()
    {
        if (snapped) return;
        if (trackingState == null) return;
        if (target == null || target.SnapCollider == null) return;
        if (!target.SnapCollider.enabled) return;

        if (trackingState.col != null && trackingState.col.IsTouching(target.SnapCollider))
            SnapNow();
    }

    /// <summary>
    /// クリア判定後、衝突検出に失敗した場合でも強制的に正解位置にスナップする
    /// </summary>
    private void ForceSnapOnClear()
    {
        if (snapped) return;
        if (trackingState == null || target == null) return;

        snapped = true;

        Vector2 a = target.Start;
        Vector2 b = target.End;

        SnapToTarget(trackingState.line, trackingState.shadow, trackingState.rb, a, b);

        if (target.SnapCollider != null)
            target.DisableSnapCollider();
    }

    private void SnapNow()
    {
        if (snapped) return;
        snapped = true;

        if (trackingState == null || target == null) return;

        Vector2 a = target.Start;
        Vector2 b = target.End;

        SnapToTarget(trackingState.line, trackingState.shadow, trackingState.rb, a, b);

        // もう役目は終わり：当たり判定は消して通過しないようにする
        target.DisableSnapCollider();
    }

    private void OnFailed()
    {
        if (resolved) return;
        resolved = true;

        if (trackingState?.line != null)
        {
            onProgress.OnNext(new FitProgress
            {
                line = trackingState.line,
                currentRate = 0f,
                maxRate = 0f,
                currentPercent = 0,
                maxPercent = 0,
                lengthOk = false,
                cleared = false,
                failed = true
            });
        }

        if (restartOnFail)
        {
            Observable.Timer(TimeSpan.FromSeconds(restartDelaySeconds))
                .Take(1)
                .Subscribe(_ => ReloadScene())
                .AddTo(this);
        }
    }

    /// <summary>
    /// 線分の「重なり率」＝ overlapLength / targetLength
    /// ※角度ズレ・平行ズレが大きい場合は 0 に落とす
    /// </summary>
    private float CalcOverlapRate(Vector2 stickStart, Vector2 stickEnd, Vector2 targetStart, Vector2 targetEnd)
    {
        Vector2 t = targetEnd - targetStart;
        float tLen = t.magnitude;
        if (tLen <= 0.0001f) return 0f;

        Vector2 u = t / tLen;

        Vector2 s = stickEnd - stickStart;
        float sLen = s.magnitude;
        if (sLen <= 0.0001f) return 0f;

        Vector2 v = s / sLen;

        // 角度許容
        float cos = Mathf.Abs(Vector2.Dot(u, v));
        float cosTh = Mathf.Cos(angleToleranceDeg * Mathf.Deg2Rad);
        if (cos < cosTh) return 0f;

        // 平行ズレ許容（見た目の太さは無視するが、判定が不可能になるので最小限だけ使う）
        float thickness = Mathf.Max(target.Thickness, 0.01f);
        float perpTol = thickness * 0.5f * Mathf.Max(perpendicularToleranceMultiplier, 0.01f);

        float d0 = DistancePointToLine(stickStart, targetStart, u);
        float d1 = DistancePointToLine(stickEnd, targetStart, u);
        if (Mathf.Max(d0, d1) > perpTol) return 0f;

        // 1D射影区間の交差長
        float p0 = Vector2.Dot(stickStart - targetStart, u);
        float p1 = Vector2.Dot(stickEnd - targetStart, u);
        float minS = Mathf.Min(p0, p1);
        float maxS = Mathf.Max(p0, p1);

        float overlap = Mathf.Max(0f, Mathf.Min(maxS, tLen) - Mathf.Max(minS, 0f));
        return Mathf.Clamp01(overlap / tLen);
    }

    private float DistancePointToLine(Vector2 p, Vector2 linePoint, Vector2 lineDirUnit)
    {
        Vector2 ap = p - linePoint;
        float cross = ap.x * lineDirUnit.y - ap.y * lineDirUnit.x;
        return Mathf.Abs(cross); // lineDirUnitが単位ベクトルなのでそのまま距離
    }

    private void SnapToTarget(LineRenderer line, LineRenderer shadow, Rigidbody2D rb, Vector2 targetStart, Vector2 targetEnd)
    {
        if (rb == null || line == null) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        Vector2 center = (targetStart + targetEnd) * 0.5f;
        Vector2 dir = targetEnd - targetStart;
        float len = dir.magnitude;
        if (len <= 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        var t = line.transform;
        t.position = new Vector3(center.x, center.y, t.position.z);
        t.rotation = Quaternion.Euler(0f, 0f, angle);

        // ピタッと：長さも正解に合わせる（±5%許容でも、成功時は正解に吸着）
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(-len * 0.5f, 0f, 0f));
        line.SetPosition(1, new Vector3( len * 0.5f, 0f, 0f));

        if (shadow != null)
        {
            shadow.useWorldSpace = false;
            shadow.positionCount = 2;
            shadow.SetPosition(0, new Vector3(-len * 0.5f, 0f, 0f));
            shadow.SetPosition(1, new Vector3( len * 0.5f, 0f, 0f));
            shadow.sortingLayerID = line.sortingLayerID;
            shadow.sortingOrder = line.sortingOrder - 1;
        }

        var box = line.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = new Vector2(len, box.size.y);
            box.offset = Vector2.zero;
        }
    }

    private void ReloadScene()
    {
        var s = SceneManager.GetActiveScene();
        SceneManager.LoadScene(s.buildIndex);
    }

    private void GetStickWorldEndpoints(LineRenderer line, out Vector2 w0, out Vector2 w1)
    {
        Vector3 p0 = line.GetPosition(0);
        Vector3 p1 = line.GetPosition(1);

        Vector3 wp0 = line.transform.TransformPoint(p0);
        Vector3 wp1 = line.transform.TransformPoint(p1);

        w0 = (Vector2)wp0;
        w1 = (Vector2)wp1;
    }

    private int ToPercent(float rate01)
    {
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(rate01) * 100f), 0, 100);
    }

    void OnDestroy()
    {
        onProgress.OnCompleted();
        onProgress.Dispose();

        onCleared.OnCompleted();
        onCleared.Dispose();
    }
}
