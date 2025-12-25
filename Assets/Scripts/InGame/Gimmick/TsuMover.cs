using UnityEngine;
using DG.Tweening;
using UniRx;
using UniRx.Triggers;

/// <summary>
/// - 「つ」を波のように上下左右へ移動（DOTween）
/// - 正解の形（伸ばし棒）は「1枚スプライト」
///   -> SpriteRenderer.sprite.bounds からローカル始点/終点を自動取得
/// - 正解の形の「始点」は固定（アンカー or 初期位置から自動決定）
/// - 正解の形の「終点」は「つ」の左側へ追従
///
/// 重要：
/// 判定(StickFitJudgeRx)が初期状態の正解線分を握りっぱなしだとズレるので、
/// 「今表示されている正解棒の始点/終点」を毎フレーム judge に渡して同期する。
/// </summary>
public class TsuMover : MonoBehaviour
{
    [Header("動かす『つ』")]
    [SerializeField, Tooltip("波移動させる『つ』のTransformを指定する。")]
    private Transform tsu;

    [Header("『つ』の波移動（ローカル座標）")]
    [SerializeField, Tooltip("左右に動く振幅（ローカルX）。")]
    private float amplitudeX = 1.0f;

    [SerializeField, Tooltip("上下に動く振幅（ローカルY）。")]
    private float amplitudeY = 0.6f;

    [SerializeField, Tooltip("左右の1往復にかかる秒数。")]
    private float periodX = 2.2f;

    [SerializeField, Tooltip("上下の1往復にかかる秒数。")]
    private float periodY = 1.4f;

    [SerializeField, Tooltip("上下運動にだけ遅延を入れて波っぽくする（0でもOK）。")]
    private float phaseDelayY = 0.2f;

    [SerializeField, Tooltip("波のイージング。Sine系が自然。")]
    private Ease waveEase = Ease.InOutSine;

    [Header("正解の形（1枚スプライト）")]
    [SerializeField, Tooltip("正解の伸ばし棒のSpriteRendererを指定する。\nこの1枚スプライトから『始点/終点』を自動で計算し、Transformを回転＆伸縮して合わせる。")]
    private SpriteRenderer correctBarSprite;

    public enum FixedEndMode
    {
        MinXIsStart,
        MaxXIsStart
    }

    [SerializeField, Tooltip("スプライトのローカル境界(b.bounds)のどちら側を『始点（固定側）』とみなすか。\n通常は左端を固定したいなら MinXIsStart。\nスプライトの向きが逆なら MaxXIsStart。")]
    private FixedEndMode fixedEndMode = FixedEndMode.MinXIsStart;

    [SerializeField, Tooltip("始点を固定するアンカー（任意）。\n未指定なら、起動時点の『正解棒スプライトの始点位置』を固定始点として自動採用する。")]
    private Transform fixedStartAnchor;

    [Header("終点を『つ』の左側へ")]
    [SerializeField, Tooltip("『つ』から左へどれだけ離すか（ワールド距離）。\n終点は tsu.position + (-tsu.right * leftDistanceFromTsu) に置かれる。")]
    private float leftDistanceFromTsu = 0.8f;

    [SerializeField, Tooltip("終点の微調整（ワールド座標オフセット）。\n『つ』の左側に来るが少し上げたい/下げたい等の調整用。")]
    private Vector3 additionalOffsetWorld = Vector3.zero;

    [Header("（任意）デバッグ表示/併用")]
    [SerializeField, Tooltip("任意：正解の棒をLineRendererでも可視化したい場合に指定。\n指定すると、始点固定・終点追従の線分を毎フレーム更新する。\n未指定でも動作には影響しない。")]
    private LineRenderer targetLine;

    [SerializeField, Tooltip("棒スプライトの『ローカルスケール=1』時のスプライト長さ（Unity単位）。\n通常は自動計算されるので触らなくてOK。\nもしスプライトが特殊（切り抜き/余白が大きい等）で長さが合わない時だけ手動調整する。")]
    private float barBaseLength = 0f; // 0なら自動算出

    [Header("クリア判定連携")]
    [SerializeField, Tooltip("クリア判定を購読するStickFitJudgeRx（未設定なら自動検出）。\nさらに、毎フレーム『今の正解線分(始点/終点)』をjudgeへ同期する（judge側に SetTargetSegment が必要）。")]
    private StickFitJudgeRx judgeRx;

    [SerializeField, Tooltip("trueにすると、毎フレーム judgeRx.SetTargetSegment(...) を呼んで\n判定が常に『表示中の正解棒』を参照するようにする。")]
    private bool syncJudgeTargetEveryFrame = true;

    // 波移動基準
    private Vector3 tsuBaseLocalPos;

    // スプライトの元のスケール（Y/Zを保つため）
    private Vector3 barBaseLocalScale;

    // 固定始点（ワールド）
    private Vector3 fixedStartWorld;

    // スプライトのローカル端点（scale=1基準）
    private Vector3 localStart;
    private Vector3 localEnd;

    // DOTween
    private Tween tweenX;
    private Tween tweenY;

    private void OnEnable()
    {
        if (tsu != null) tsuBaseLocalPos = tsu.localPosition;

        if (correctBarSprite != null)
        {
            barBaseLocalScale = correctBarSprite.transform.localScale;
            CacheLocalEndpointsFromSprite();

            fixedStartWorld = (fixedStartAnchor != null)
                ? fixedStartAnchor.position
                : correctBarSprite.transform.TransformPoint(localStart);
        }

        if (judgeRx == null)
        {
            var judges = UnityEngine.Object.FindObjectsByType<StickFitJudgeRx>(UnityEngine.FindObjectsSortMode.None);
            if (judges.Length > 0) judgeRx = judges[0];
        }

        StartWave();
        StartFollowStream();
        StartClearSubscription();
    }

    private void OnDisable()
    {
        KillWave();
    }

    private void CacheLocalEndpointsFromSprite()
    {
        if (correctBarSprite == null || correctBarSprite.sprite == null) return;

        var b = correctBarSprite.sprite.bounds;

        float y = b.center.y;
        float z = 0f;

        Vector3 pMin = new Vector3(b.min.x, y, z);
        Vector3 pMax = new Vector3(b.max.x, y, z);

        if (fixedEndMode == FixedEndMode.MinXIsStart)
        {
            localStart = pMin;
            localEnd = pMax;
        }
        else
        {
            localStart = pMax;
            localEnd = pMin;
        }

        float autoLen = Mathf.Abs(pMax.x - pMin.x);
        if (barBaseLength <= 0.0001f) barBaseLength = autoLen;
    }

    private void StartWave()
    {
        KillWave();
        if (tsu == null) return;

        tweenX = tsu.DOLocalMoveX(tsuBaseLocalPos.x + amplitudeX, periodX)
            .SetEase(waveEase)
            .SetLoops(-1, LoopType.Yoyo);

        tweenY = tsu.DOLocalMoveY(tsuBaseLocalPos.y + amplitudeY, periodY)
            .SetDelay(phaseDelayY)
            .SetEase(waveEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void KillWave()
    {
        tweenX?.Kill();
        tweenY?.Kill();
        tweenX = null;
        tweenY = null;
    }

    private void StartFollowStream()
    {
        this.LateUpdateAsObservable()
            .Where(_ => tsu != null && correctBarSprite != null && correctBarSprite.sprite != null)
            .Subscribe(_ =>
            {
                // 追従したい終点（ワールド）
                Vector3 endWorld = tsu.position + (-tsu.right * leftDistanceFromTsu) + additionalOffsetWorld;

                // 「固定始点 -> endWorld」に合わせてバーTransform更新
                ApplyBarTransformByEndpoints(fixedStartWorld, endWorld);

                // 今「表示されている」正解棒の始点/終点（ワールド）を取り直す
                // ※判定に渡すのはこれが正しい（初期配置ではない）
                Vector3 startNowWorld = correctBarSprite.transform.TransformPoint(localStart);
                Vector3 endNowWorld = correctBarSprite.transform.TransformPoint(localEnd);

                // 任意：LineRendererで可視化
                if (targetLine != null)
                {
                    targetLine.positionCount = 2;
                    targetLine.useWorldSpace = true;
                    targetLine.SetPosition(0, startNowWorld);
                    targetLine.SetPosition(1, endNowWorld);
                }

                // ★ここが重要：判定へ「最新の正解線分」を同期
                if (syncJudgeTargetEveryFrame && judgeRx != null)
                {
                    // judgeRx 側に SetTargetSegment が必要（下に追加コードあり）
                    judgeRx.SetTargetSegment(startNowWorld, endNowWorld);
                }
            })
            .AddTo(this);
    }

    private void StartClearSubscription()
    {
        if (judgeRx == null) return;

        judgeRx.OnClearedAsObservable
            .Take(1)
            .Subscribe(_ =>
            {
                Debug.Log("[TsuMover] クリア判定を受けたため『つ』の移動を停止します。");
                KillWave();
            })
            .AddTo(this);
    }

    private void ApplyBarTransformByEndpoints(Vector3 startWorld, Vector3 endWorld)
    {
        var t = correctBarSprite.transform;

        Vector3 dir = endWorld - startWorld;
        float dist = dir.magnitude;
        if (dist <= 0.0001f || barBaseLength <= 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        float scaleFactorX = dist / barBaseLength;
        Vector3 newScale = new Vector3(barBaseLocalScale.x * scaleFactorX, barBaseLocalScale.y, barBaseLocalScale.z);

        t.rotation = rot;
        t.localScale = newScale;

        Vector3 worldOfLocalStartNow = t.TransformPoint(localStart);
        Vector3 delta = startWorld - worldOfLocalStartNow;
        t.position += delta;
    }
}
