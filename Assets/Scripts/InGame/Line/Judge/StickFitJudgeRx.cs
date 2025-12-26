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

    // [Header("失敗時：自動リスタート")]
    // [SerializeField] private bool restartOnFail = true;
    // [SerializeField] private float restartDelaySeconds = 0.5f;

    [Header("失敗判定（画面外に落ちた場合）")]
    [SerializeField] private float failYThreshold = -10f;

    // [Header("失敗判定（棒が停止した場合）")]
    // [SerializeField] private float stoppedVelocityThreshold = 0.1f;
    // [SerializeField] private float stoppedTimeTolerance = 0.5f;
    // [SerializeField] private float failureJudgmentDelaySeconds = 1f;

    [Header("クリア後のスナップ項目 (チェックボックス)")]
    [SerializeField, Tooltip("標準では全てオン。個別にオフで挙動を保持。")]
    private bool snapLengthToTarget = true;
    [SerializeField] private bool alignXPosition = true;
    [SerializeField] private bool alignYPosition = true;
    [SerializeField, Tooltip("2DではZ回転を合わせます")] private bool alignRotationAngles = true;

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
    private readonly Subject<Unit> onFailed = new Subject<Unit>();

    public IObservable<FitProgress> OnProgressAsObservable => onProgress;
    public IObservable<Unit> OnClearedAsObservable => onCleared;
    public IObservable<Unit> OnFailedAsObservable => onFailed;

    private bool resolved;
    // private bool enableFailureJudgment;

    // クリアは「通知」と「スナップ完了」は別扱い
    private bool snapped;

    // 外部から判定を一時停止する機能（Stage7の点滅ギミック用）
    private bool judgmentSuspended = false;

    private Vector2 targetStartWorld;
    private Vector2 targetEndWorld;
    private bool hasDynamicTarget;

    /// <summary>
    /// 判定を一時停止/再開する（非表示中は判定しない）
    /// </summary>
    public void SetJudgmentSuspended(bool suspended)
    {
        judgmentSuspended = suspended;
    }

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

    // private TrackingState trackingState; // 廃止
    private readonly System.Collections.Generic.List<TrackingState> trackingStates = new System.Collections.Generic.List<TrackingState>();

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

        // チェックボックスはデフォルト値（全てオン）でそのまま使用します

        drawer.OnPressUpAsObservable
            .Where(_ => !resolved)
            .Subscribe(x => StartTracking(x.line, x.shadow))
            .AddTo(this);
            
        // 判定ループ（解決するまで回す）
        Observable.EveryFixedUpdate()
            .Where(_ => !resolved)
            .Subscribe(_ => TickJudge())
            .AddTo(this);
    }

    private void StartTracking(LineRenderer line, LineRenderer shadow)
    {
        if (line == null) return;

        // 失敗判定は無効化のまま（ゲームオーバーにしない）
        snapped = false;

        // Rigidbody2D が付くまで待つ（LineDropOnRelease 側で付与される）
        Observable.EveryFixedUpdate()
            .Select(_ => line.GetComponent<Rigidbody2D>())
            .Where(rb => rb != null)
            .Take(1)
            .Subscribe(rb =>
            {
                var state = new TrackingState
                {
                    line = line,
                    shadow = shadow,
                    rb = rb,
                    col = line.GetComponent<Collider2D>(),
                    maxRate = 0f,
                    lengthOk = false,
                    stoppedTime = 0f
                };

                trackingStates.Add(state);
            })
            .AddTo(this);
    }

    private void TickJudge()
    {
        if (resolved) return;
        if (target == null || !target.IsValid) return;
        if (judgmentSuspended) return; // 判定が一時停止中はスキップ

        if (!target.gameObject.activeInHierarchy)
        {
            // ターゲット不在なら判定不能だが、Failはしない
            return;
        }

        Vector2 targetStart = hasDynamicTarget ? targetStartWorld : target.Start;
        Vector2 targetEnd = hasDynamicTarget ? targetEndWorld : target.End;
        float targetLen = Vector2.Distance(targetStart, targetEnd);
        
        // 全てのスティックを走査
        // 逆順ループ（削除対応のため）
        for (int i = trackingStates.Count - 1; i >= 0; i--)
        {
            var state = trackingStates[i];
            
            // 削除済み or 破壊済みならリストから除外
            if (state.line == null || state.rb == null)
            {
                trackingStates.RemoveAt(i);
                continue;
            }

            // 画面外落下判定（失敗ではなく、単に追跡終了）
            if (state.line.transform.position.y < failYThreshold)
            {
                trackingStates.RemoveAt(i);
                // 失敗UIなどは出さない
                continue;
            }

            // 棒の端点（ワールド）
            GetStickWorldEndpoints(state.line, out Vector2 stickStart, out Vector2 stickEnd);
            float stickLen = Vector2.Distance(stickStart, stickEnd);

            if (stickLen <= 0.0001f || targetLen <= 0.0001f)
            {
                state.lengthOk = false;
                continue;
            }

            // 長さ許容（±5%）
            float ratio = stickLen / targetLen;
            float minRatio = 1f - lengthTolerancePercent;
            float maxRatio = 1f + lengthTolerancePercent;
            state.lengthOk = (ratio >= minRatio && ratio <= maxRatio);

            // ハマリ率：線分の重なり率（長さOKの瞬間だけ max を更新）
            float fitRate = state.lengthOk ? CalcOverlapRate(stickStart, stickEnd, targetStart, targetEnd) : 0f;
            if (fitRate > state.maxRate) state.maxRate = fitRate;
            
            // UI通知（最新の操作対象、もしくは一番いいスコアのやつなどを送るのが理想だが、
            // ここではとりあえず「最後に投げたやつ」または「クリア条件満たしたやつ」を優先したい）
            // 簡易的に「更新があったら」送るが、複数あるとUIがチラつく。
            // いったん「最新（リスト末尾）」の状態を送るようにしてみる。
            bool isLatest = (i == trackingStates.Count - 1);
            if (isLatest)
            {
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
            }

            // ✅ クリア条件：長さOK かつ maxRate >= 0.95（±5%）
            float clearThreshold = 1f - lengthTolerancePercent;
            if (state.lengthOk && state.maxRate >= clearThreshold)
            {
                SignalClear(state);
                return;
            }
        }
    }

    /// <summary>
    /// クリアを「通知」する（この瞬間に CLEAR! にしたい）
    /// その後、SnapCollider を有効化して接触したら停止＆スナップ。
    /// </summary>
    private void SignalClear(TrackingState winningState)
    {
        // 非表示期間など、判定一時停止中はクリア通知しない
        if (resolved || judgmentSuspended) return;
        resolved = true;

        if (target != null) target.EnableSnapCollider();

        // Presenter 側は cleared=true を受けて CLEAR! 表示に切り替える
        onProgress.OnNext(new FitProgress
        {
            line = winningState.line,
            currentRate = 1f,
            maxRate = 1f,
            currentPercent = 100,
            maxPercent = 100,
            lengthOk = true,
            cleared = true,
            failed = false
        });

        onCleared.OnNext(Unit.Default);

        // スコアアタックモード中ならステージクリアを通知（ただしリザルト表示後は進めない）
        if (ScoreAttackManager.Instance != null && ScoreAttackManager.Instance.IsRunning.Value && !ScoreAttackManager.Instance.IsResultShown)
        {
            ScoreAttackManager.Instance.OnStageCleared();
        }
        
        // クリア条件を満たした瞬間に即スナップする（衝突待ちは不安定なため）
        SnapNow(winningState);
    }

    private void SnapNow(TrackingState state)
    {
        if (snapped) return;
        snapped = true;

        if (state == null || state.line == null || target == null) return;

        // 現在の正解形（動的更新がある場合は最新のワールド座標）
        Vector2 a = hasDynamicTarget ? targetStartWorld : target.Start;
        Vector2 b = hasDynamicTarget ? targetEndWorld : target.End;

        SnapToTarget(state.line, state.shadow, state.rb, a, b);

        // もう役目は終わり：当たり判定は消して通過しないようにする
        target.DisableSnapCollider();
    }

    private void OnFailed()
    {
        // 廃止されました（失敗判定なし）
        // if (resolved) return;
        // resolved = true;
        // onFailed.OnNext(Unit.Default);
        
        // 旧処理: 一番最後のスティックでも取ってFAIL通知していたが、今は何もしない。
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

        // 物理を停止
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        // ターゲット情報
        Vector2 targetCenter = (targetStart + targetEnd) * 0.5f;
        Vector2 targetDir = targetEnd - targetStart;
        float targetLen = targetDir.magnitude;
        if (targetLen <= 0.0001f) return;
        float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;

        // 現在の棒情報（ワールド）
        GetStickWorldEndpoints(line, out Vector2 curStart, out Vector2 curEnd);
        float currentLen = Vector2.Distance(curStart, curEnd);
        Vector2 currentCenter = (curStart + curEnd) * 0.5f;
        Vector2 currentDir = curEnd - curStart;
        float currentAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;

        // 長さを決定
        float snapLen = snapLengthToTarget ? targetLen : currentLen;
        if (snapLen <= 0.0001f) snapLen = targetLen; // フェイルセーフ

        // 位置（中心）を決定
        float snapX = alignXPosition ? targetCenter.x : currentCenter.x;
        float snapY = alignYPosition ? targetCenter.y : currentCenter.y;
        Vector2 snapCenter = new Vector2(snapX, snapY);

        // 角度を決定（2DではZ回転）
        float snapAngle = alignRotationAngles ? targetAngle : currentAngle;

        // 反映（スケール系ギミックの影響を無効化してから適用）
        var t = line.transform;
        t.localScale = Vector3.one;
        t.position = new Vector3(snapCenter.x, snapCenter.y, t.position.z);
        t.rotation = Quaternion.Euler(0f, 0f, snapAngle);

        // 長さ反映（ローカル座標で線分を更新）
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, new Vector3(-snapLen * 0.5f, 0f, 0f));
        line.SetPosition(1, new Vector3( snapLen * 0.5f, 0f, 0f));

        if (shadow != null)
        {
            shadow.transform.localScale = Vector3.one;
            shadow.useWorldSpace = false;
            shadow.positionCount = 2;
            shadow.SetPosition(0, new Vector3(-snapLen * 0.5f, 0f, 0f));
            shadow.SetPosition(1, new Vector3( snapLen * 0.5f, 0f, 0f));
            shadow.sortingLayerID = line.sortingLayerID;
            shadow.sortingOrder = line.sortingOrder - 1;
        }

        var box = line.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = new Vector2(snapLen, box.size.y);
            box.offset = Vector2.zero;
        }

        // クリア後はTriggerScaleByTagなどのギミックに反応しないよう、タグを"Untagged"に変更
        line.gameObject.tag = "Untagged";
        if (shadow != null)
        {
            shadow.gameObject.tag = "Untagged";
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

    // TsuMoverから毎フレーム呼ばれる
    public void SetTargetSegment(Vector3 startWorld, Vector3 endWorld)
    {
        targetStartWorld = startWorld;
        targetEndWorld = endWorld;
        hasDynamicTarget = true;
    }

    void OnDestroy()
    {
        onProgress.OnCompleted();
        onProgress.Dispose();

        onCleared.OnCompleted();
        onCleared.Dispose();

        onFailed.OnCompleted();
        onFailed.Dispose();
    }
}
