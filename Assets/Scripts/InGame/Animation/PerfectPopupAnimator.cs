using UnityEngine;
using UniRx;
using DG.Tweening;

/// <summary>
/// SpriteRenderer版「ピッタリ！」演出
/// - 非アクティブ開始OK
/// - OnEnableで購読
/// - 有効化直後の1フレームを無視してから再生
/// - 本体＋影の親オブジェクトをまとめて操作
/// </summary>
public class PerfectPopupAnimator : MonoBehaviour
{
    [Header("購読元")]
    [SerializeField] private StickFitJudgeRx fitJudge;

    [Header("本体・影の親")]
    [SerializeField] private Transform mainRoot;
    [SerializeField] private Transform shadowRoot;

    [Header("移動")]
    [SerializeField] private float floatDistance = 0.6f;
    [SerializeField] private float moveDuration = 0.25f;
    [SerializeField] private Ease moveEase = Ease.OutBack;

    [Header("フェード")]
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float displayDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    [Header("スケール（任意）")]
    [SerializeField] private bool useScale = true;
    [SerializeField] private float startScaleMultiplier = 0.85f;
    [SerializeField] private float scaleDuration = 0.2f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    private SpriteRenderer[] mainRenderers;
    private SpriteRenderer[] shadowRenderers;

    private Vector3 mainStartPos;
    private Vector3 shadowStartPos;
    private Vector3 mainStartScale;
    private Vector3 shadowStartScale;

    private Sequence seq;
    private bool initialized;

    /* =========================
     * Lifecycle
     * ========================= */

    void OnEnable()
    {
        if (!initialized)
            Initialize();

        // 非アクティブ → アクティブ直後の1フレームを無視
        Observable.NextFrame()
            .Subscribe(_ => SubscribeClear())
            .AddTo(this);
    }

    void OnDisable()
    {
        seq?.Kill();
    }

    void OnDestroy()
    {
        seq?.Kill();
    }

    /* =========================
     * 初期化
     * ========================= */

    private void Initialize()
    {
        if (fitJudge == null || mainRoot == null || shadowRoot == null)
        {
            Debug.LogError("PerfectPopupSpriteAnimatorRx の参照が未設定");
            return;
        }

        mainRenderers = mainRoot.GetComponentsInChildren<SpriteRenderer>(true);
        shadowRenderers = shadowRoot.GetComponentsInChildren<SpriteRenderer>(true);

        mainStartPos = mainRoot.position;
        shadowStartPos = shadowRoot.position;
        mainStartScale = mainRoot.localScale;
        shadowStartScale = shadowRoot.localScale;

        // 完全初期状態
        SetAlpha(mainRenderers, 0f);
        SetAlpha(shadowRenderers, 0f);

        mainRoot.gameObject.SetActive(false);
        shadowRoot.gameObject.SetActive(false);

        initialized = true;
    }

    private void SubscribeClear()
    {
        fitJudge.OnClearedAsObservable
            .Take(1)
            .Subscribe(_ => Play())
            .AddTo(this);
    }

    /* =========================
     * 再生
     * ========================= */

    private void Play()
    {
        seq?.Kill();

        mainRoot.gameObject.SetActive(true);
        shadowRoot.gameObject.SetActive(true);

        mainRoot.position = mainStartPos - Vector3.up * floatDistance;
        shadowRoot.position = shadowStartPos - Vector3.up * floatDistance;

        if (useScale)
        {
            mainRoot.localScale = mainStartScale * startScaleMultiplier;
            shadowRoot.localScale = shadowStartScale * startScaleMultiplier;
        }
        else
        {
            mainRoot.localScale = mainStartScale;
            shadowRoot.localScale = shadowStartScale;
        }

        SetAlpha(mainRenderers, 0f);
        SetAlpha(shadowRenderers, 0f);

        seq = DOTween.Sequence();

        // フェードイン
        seq.Append(FadeRenderers(mainRenderers, 1f, fadeInDuration));
        seq.Join(FadeRenderers(shadowRenderers, 1f, fadeInDuration));

        // 浮き上がり
        seq.Join(mainRoot.DOMove(mainStartPos, moveDuration).SetEase(moveEase));
        seq.Join(shadowRoot.DOMove(shadowStartPos, moveDuration).SetEase(moveEase));

        // スケール
        if (useScale)
        {
            seq.Join(mainRoot.DOScale(mainStartScale, scaleDuration).SetEase(scaleEase));
            seq.Join(shadowRoot.DOScale(shadowStartScale, scaleDuration).SetEase(scaleEase));
        }

        // 表示維持
        seq.AppendInterval(displayDuration);

        // フェードアウト
        seq.Append(FadeRenderers(mainRenderers, 0f, fadeOutDuration));
        seq.Join(FadeRenderers(shadowRenderers, 0f, fadeOutDuration));

        seq.OnComplete(() =>
        {
            mainRoot.gameObject.SetActive(false);
            shadowRoot.gameObject.SetActive(false);

            // 念のため復元
            mainRoot.position = mainStartPos;
            shadowRoot.position = shadowStartPos;
            mainRoot.localScale = mainStartScale;
            shadowRoot.localScale = shadowStartScale;
        });
    }

    /* =========================
     * Utility
     * ========================= */

    private static void SetAlpha(SpriteRenderer[] renderers, float a)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var c = r.color;
            c.a = a;
            r.color = c;
        }
    }

    private static Tween FadeRenderers(SpriteRenderer[] renderers, float to, float duration)
    {
        float current = (renderers != null && renderers.Length > 0 && renderers[0] != null)
            ? renderers[0].color.a
            : 0f;

        return DOTween.To(
            () => current,
            x =>
            {
                current = x;
                SetAlpha(renderers, current);
            },
            to,
            duration
        ).SetEase(Ease.Linear);
    }
}
