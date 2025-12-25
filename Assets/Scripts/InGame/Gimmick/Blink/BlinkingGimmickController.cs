using System;
using UnityEngine;
using UniRx;
using DG.Tweening;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Stage7専用：「つ」と「正解位置」を点滅させるギミック
/// - 表示中（透明度100%）のみ、クリアの表示やコライダーを有効化
/// - 非表示中（透明度0%）は無効化
/// - DOTweenで透明度をアニメーション
/// - UniRxで状態を管理
/// </summary>
public class BlinkingGimmickController : MonoBehaviour
{
    [System.Serializable]
    public class BlinkItem
    {
        [Tooltip("点滅させる対象オブジェクト")] public GameObject target;
        [Tooltip("可視時アルファ(0-255)")] [Range(0, 255)] public int visibleAlpha255 = 255;
        [HideInInspector] public CanvasGroup canvasGroup;
        [HideInInspector] public SpriteRenderer[] spriteRenderers;
    }

    [Header("点滅対象")]
    [SerializeField, Tooltip("点滅対象のリスト（設定されていればこちらを優先）")]
    private List<BlinkItem> blinkItems = new List<BlinkItem>();
    [SerializeField, Tooltip("点滅させる「つ」オブジェクト")]
    private GameObject tsuObject;

    [SerializeField, Tooltip("点滅させる「正解位置」オブジェクト")]
    private GameObject targetObject;

    [Header("点滅設定")]
    [SerializeField, Tooltip("表示時間（秒）")]
    private float visibleDuration = 1.0f;

    [SerializeField, Tooltip("非表示時間（秒）")]
    private float invisibleDuration = 1.0f;

    [SerializeField, Tooltip("フェードイン/アウトの速度（秒）")]
    private float fadeDuration = 0.3f;

    [SerializeField, Tooltip("つの可視時アルファ(0-255)")]
    [Range(0, 255)] private int tsuVisibleAlpha255 = 255;

    [SerializeField, Tooltip("正解位置の可視時アルファ(0-255)")]
    [Range(0, 255)] private int targetVisibleAlpha255 = 255;

    [SerializeField, Tooltip("自動開始するか")]
    private bool autoStart = true;

    [Header("有効化制御")]
    [SerializeField, Tooltip("表示中に有効化するコライダー（複数可）")]
    private Collider2D[] collidersToControl;

    [SerializeField, Tooltip("表示中に有効化するGameObject（複数可）")]
    private GameObject[] objectsToControl;

    [SerializeField, Tooltip("非表示中に判定を停止するStickFitJudgeRx（判定を完全に停止）")]
    private StickFitJudgeRx[] judgeRxToControl;

    // 現在表示中かどうかを外部に公開
    private readonly ReactiveProperty<bool> isVisible = new ReactiveProperty<bool>(false);
    public IReadOnlyReactiveProperty<bool> IsVisibleAsObservable => isVisible;

    // 点滅制御用
    private IDisposable blinkingDisposable;
    private CanvasGroup tsuCanvasGroup;
    private CanvasGroup targetCanvasGroup;
    private SpriteRenderer[] tsuSpriteRenderers;
    private SpriteRenderer[] targetSpriteRenderers;

    void Awake()
    {
        // CanvasGroupまたはSpriteRendererを取得・準備
        SetupTransparencyComponents();

        // 判定制御対象が未設定なら自動検出
        if (judgeRxToControl == null || judgeRxToControl.Length == 0)
        {
            judgeRxToControl = UnityEngine.Object.FindObjectsByType<StickFitJudgeRx>(UnityEngine.FindObjectsSortMode.None);
        }
    }

    void Start()
    {
        if (autoStart)
        {
            StartBlinking();
        }

        // 表示状態に応じてコライダーやオブジェクトを制御
        isVisible
            .Subscribe(visible => UpdateControlledObjects(visible))
            .AddTo(this);
    }

    /// <summary>
    /// 透明度制御用のコンポーネントをセットアップ
    /// </summary>
    private void SetupTransparencyComponents()
    {
        // リスト側のセットアップ
        if (blinkItems != null && blinkItems.Count > 0)
        {
            foreach (var item in blinkItems)
            {
                if (item == null || item.target == null) continue;
                item.canvasGroup = item.target.GetComponent<CanvasGroup>();
                if (item.canvasGroup == null && item.target.GetComponent<Canvas>() != null)
                {
                    item.canvasGroup = item.target.AddComponent<CanvasGroup>();
                }
                item.spriteRenderers = item.target.GetComponentsInChildren<SpriteRenderer>(true);
            }
        }

        if (tsuObject != null)
        {
            tsuCanvasGroup = tsuObject.GetComponent<CanvasGroup>();
            if (tsuCanvasGroup == null && tsuObject.GetComponent<Canvas>() != null)
            {
                tsuCanvasGroup = tsuObject.AddComponent<CanvasGroup>();
            }

            tsuSpriteRenderers = tsuObject.GetComponentsInChildren<SpriteRenderer>(true);
        }

        if (targetObject != null)
        {
            targetCanvasGroup = targetObject.GetComponent<CanvasGroup>();
            if (targetCanvasGroup == null && targetObject.GetComponent<Canvas>() != null)
            {
                targetCanvasGroup = targetObject.AddComponent<CanvasGroup>();
            }

            targetSpriteRenderers = targetObject.GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    void OnDestroy()
    {
        blinkingDisposable?.Dispose();
        // DOTWEENの安全対策：リンク済みTweenを殺す
        KillAllItemTweens();

        // 判定を停止しておく
        if (judgeRxToControl != null)
        {
            foreach (var judgeRx in judgeRxToControl)
            {
                if (judgeRx != null) judgeRx.SetJudgmentSuspended(true);
            }
        }
    }

    void OnDisable()
    {
        // 無効化時にもTweenを停止
        KillAllItemTweens();
    }
    /// <summary>
    /// 点滅を開始
    /// </summary>
    public void StartBlinking()
    {
        blinkingDisposable?.Dispose();

        // 初期状態を非表示に設定
        if (HasItems())
        {
            SetTransparencyForItems(false);
        }
        else
        {
            SetTransparency(0f, 0f);
        }
        isVisible.Value = false;

        // 点滅ループ
        blinkingDisposable = Observable
            .Interval(System.TimeSpan.FromSeconds(visibleDuration + invisibleDuration + fadeDuration * 2))
            .StartWith(0) // 最初のサイクルを即座に開始
            .Subscribe(_ => BlinkCycle())
            .AddTo(this);
    }

    /// <summary>
    /// 点滅を停止
    /// </summary>
    public void StopBlinking()
    {
        blinkingDisposable?.Dispose();
        if (HasItems())
        {
            SetTransparencyForItems(true);
        }
        else
        {
            SetTransparency(Norm255(tsuVisibleAlpha255), Norm255(targetVisibleAlpha255));
        }
        isVisible.Value = true;
    }

    /// <summary>
    /// 1サイクルの点滅処理
    /// </summary>
    private void BlinkCycle()
    {
        var sequence = DOTween.Sequence();

        // フェードイン
        sequence.AppendCallback(() =>
        {
            KillAllItemTweens();
            if (HasItems())
            {
                AnimateTransparencyForItems(true, fadeDuration);
            }
            else
            {
                AnimateTransparency(Norm255(tsuVisibleAlpha255), Norm255(targetVisibleAlpha255), fadeDuration);
            }
            isVisible.Value = true;
        });

        sequence.AppendInterval(fadeDuration);

        // 表示状態を維持
        sequence.AppendInterval(visibleDuration);

        // フェードアウト
        sequence.AppendCallback(() =>
        {
            if (HasItems())
            {
                AnimateTransparencyForItems(false, fadeDuration);
            }
            else
            {
                AnimateTransparency(0f, 0f, fadeDuration);
            }
            isVisible.Value = false;
        });

        sequence.AppendInterval(fadeDuration);

        // 非表示状態を維持
        sequence.AppendInterval(invisibleDuration);
    }

    /// <summary>
    /// 透明度をアニメーション
    /// </summary>
    private void AnimateTransparency(float tsuAlpha, float targetAlpha, float duration)
    {
        // CanvasGroupがある場合
        if (tsuCanvasGroup != null)
        {
            tsuCanvasGroup.DOFade(tsuAlpha, duration)
                .SetLink(tsuCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else if (tsuSpriteRenderers != null && tsuSpriteRenderers.Length > 0)
        {
            // SpriteRendererの場合（nullチェックを追加）
            foreach (var sr in tsuSpriteRenderers)
            {
                if (sr != null)
                {
                    sr.DOFade(tsuAlpha, duration)
                        .SetLink(sr.gameObject, LinkBehaviour.KillOnDestroy);
                }
            }
        }

        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.DOFade(targetAlpha, duration)
                .SetLink(targetCanvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
        }
        else if (targetSpriteRenderers != null && targetSpriteRenderers.Length > 0)
        {
            // SpriteRendererの場合（nullチェックを追加）
            foreach (var sr in targetSpriteRenderers)
            {
                if (sr != null)
                {
                    sr.DOFade(targetAlpha, duration)
                        .SetLink(sr.gameObject, LinkBehaviour.KillOnDestroy);
                }
            }
        }
    }

    private void AnimateTransparencyForItems(bool toVisible, float duration)
    {
        if (!HasItems()) return;
        foreach (var item in blinkItems)
        {
            if (item == null || item.target == null) continue;
            float alpha = toVisible ? Norm255(item.visibleAlpha255) : 0f;

            if (item.canvasGroup != null)
            {
                item.canvasGroup.DOFade(alpha, duration)
                    .SetLink(item.canvasGroup.gameObject, LinkBehaviour.KillOnDestroy);
            }
            else if (item.spriteRenderers != null && item.spriteRenderers.Length > 0)
            {
                foreach (var sr in item.spriteRenderers)
                {
                    if (sr != null)
                    {
                        sr.DOFade(alpha, duration)
                            .SetLink(sr.gameObject, LinkBehaviour.KillOnDestroy);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 透明度を即座に設定
    /// </summary>
    private void SetTransparency(float tsuAlpha, float targetAlpha)
    {
        if (tsuCanvasGroup != null)
        {
            tsuCanvasGroup.alpha = tsuAlpha;
        }
        else if (tsuSpriteRenderers != null)
        {
            foreach (var sr in tsuSpriteRenderers)
            {
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = tsuAlpha;
                    sr.color = c;
                }
            }
        }

        if (targetCanvasGroup != null)
        {
            targetCanvasGroup.alpha = targetAlpha;
        }
        else if (targetSpriteRenderers != null)
        {
            foreach (var sr in targetSpriteRenderers)
            {
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = targetAlpha;
                    sr.color = c;
                }
            }
        }
    }

    private void SetTransparencyForItems(bool toVisible)
    {
        if (!HasItems()) return;
        foreach (var item in blinkItems)
        {
            if (item == null || item.target == null) continue;
            float alpha = toVisible ? Norm255(item.visibleAlpha255) : 0f;

            if (item.canvasGroup != null)
            {
                item.canvasGroup.alpha = alpha;
            }
            else if (item.spriteRenderers != null)
            {
                foreach (var sr in item.spriteRenderers)
                {
                    if (sr != null)
                    {
                        var c = sr.color;
                        c.a = alpha;
                        sr.color = c;
                    }
                }
            }
        }
    }

    private static float Norm255(int a)
    {
        return Mathf.Clamp01(a / 255f);
    }

    private bool HasItems()
    {
        return blinkItems != null && blinkItems.Count > 0;
    }

    private void KillAllItemTweens()
    {
        if (!HasItems())
        {
            if (tsuObject != null) DOTween.Kill(tsuObject);
            if (targetObject != null) DOTween.Kill(targetObject);
            return;
        }
        foreach (var item in blinkItems)
        {
            if (item?.target != null) DOTween.Kill(item.target);
        }
    }

    /// <summary>
    /// 制御対象のコライダー・オブジェクトを有効/無効化
    /// </summary>
    private void UpdateControlledObjects(bool visible)
    {
        if (collidersToControl != null)
        {
            foreach (var col in collidersToControl)
            {
                if (col != null)
                {
                    col.enabled = visible;
                }
            }
        }

        if (objectsToControl != null)
        {
            foreach (var obj in objectsToControl)
            {
                if (obj != null)
                {
                    // SetActiveではなく、CanvasGroupやSpriteRendererの有効化で制御
                    var cg = obj.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = visible ? 1f : 0f;
                        cg.interactable = visible;
                        cg.blocksRaycasts = visible;
                    }
                    else
                    {
                        // SpriteRendererの場合
                        var renderers = obj.GetComponentsInChildren<SpriteRenderer>();
                        foreach (var sr in renderers)
                        {
                            sr.enabled = visible;
                        }
                    }
                }
            }
        }

        // StickFitJudgeRxの判定を制御して、非表示中は判定を完全停止
        if (judgeRxToControl != null)
        {
            foreach (var judgeRx in judgeRxToControl)
            {
                if (judgeRx != null)
                {
                    judgeRx.SetJudgmentSuspended(!visible);
                }
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (visibleDuration < 0.1f) visibleDuration = 0.1f;
        if (invisibleDuration < 0.1f) invisibleDuration = 0.1f;
        if (fadeDuration < 0.05f) fadeDuration = 0.05f;
    }
#endif
}
