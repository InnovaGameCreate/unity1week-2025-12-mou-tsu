using UnityEngine;
using UnityEngine.UI;
using UniRx;
using DG.Tweening;

public class CreditToggle : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private GameObject creditPanel;

    [Header("表示基準スケール")]
    [Tooltip("素材を4倍で作ったなら 0.25。8倍なら 0.125。表示上の「等倍」をここで決める。")]
    [SerializeField] private float baseScale = 0.25f;

    [Header("アニメ設定（※ baseScale に対する倍率）")]
    [Tooltip("開き始め：baseScale * openFromScale から baseScale へ")]
    [SerializeField] private float openDuration = 0.25f;
    [SerializeField] private float closeDuration = 0.18f;
    [SerializeField] private float openFromScale = 0.24f;   // baseScale に掛ける倍率
    [SerializeField] private float closeToScale = 0.23f;    // baseScale に掛ける倍率
    [SerializeField] private Ease openEase = Ease.OutQuad;
    [SerializeField] private Ease closeEase = Ease.InQuad;

    [Header("任意：背景クリックで閉じる(保険)")]
    [SerializeField] private Button panelBackgroundCloseButton;

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    private Tween panelTween;

    private enum PanelState { Closed, Opening, Open, Closing }
    private readonly ReactiveProperty<PanelState> state = new ReactiveProperty<PanelState>(PanelState.Closed);

    // Closed 以外は true（閉じ切るまで true のまま）
    public bool IsOpen => state.Value != PanelState.Closed;

    private ReadOnlyReactiveProperty<bool> isOpenRx;
    public IReadOnlyReactiveProperty<bool> IsOpenRx => isOpenRx;

    private void Awake()
    {
        if (creditPanel == null)
        {
            Debug.LogError("[CreditToggle] creditPanel が未設定です");
            return;
        }

        if (baseScale <= 0f)
        {
            Debug.LogWarning("[CreditToggle] baseScale は 0 より大きい必要があります。1 に補正します。");
            baseScale = 1f;
        }

        canvasGroup = creditPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = creditPanel.AddComponent<CanvasGroup>();

        panelRect = creditPanel.GetComponent<RectTransform>();

        ApplyClosedInstant();

        isOpenRx = state
            .Select(s => s != PanelState.Closed)
            .DistinctUntilChanged()
            .ToReadOnlyReactiveProperty()
            .AddTo(this);

        if (panelBackgroundCloseButton != null)
        {
            panelBackgroundCloseButton
                .OnClickAsObservable()
                .Subscribe(_ => Close())
                .AddTo(this);
        }
    }

    public void Toggle()
    {
        // アニメ中の連打（または二重発火）を無視
        if (state.Value == PanelState.Opening || state.Value == PanelState.Closing) return;

        if (state.Value == PanelState.Closed) Open();
        else Close();
    }

    public void Open()
    {
        if (creditPanel == null) return;
        if (state.Value == PanelState.Open || state.Value == PanelState.Opening) return;

        KillTween();

        creditPanel.SetActive(true);

        // 開き始めの見た目
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        // baseScale を基準に「開き始め倍率」を掛ける
        if (panelRect != null)
            panelRect.localScale = Vector3.one * (baseScale * openFromScale);

        state.Value = PanelState.Opening;

        var seq = DOTween.Sequence();
        seq.Join(canvasGroup.DOFade(1f, openDuration).SetEase(openEase));

        // 開いた最終状態は baseScale（＝表示上の等倍）
        if (panelRect != null)
            seq.Join(panelRect.DOScale(baseScale, openDuration).SetEase(openEase));

        seq.OnComplete(() => state.Value = PanelState.Open);

        panelTween = seq.SetLink(creditPanel, LinkBehaviour.KillOnDestroy);
    }

    public void Close()
    {
        if (creditPanel == null) return;
        if (state.Value == PanelState.Closed || state.Value == PanelState.Closing) return;

        KillTween();

        // 閉じ中も裏を押せない
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        state.Value = PanelState.Closing;

        var seq = DOTween.Sequence();
        seq.Join(canvasGroup.DOFade(0f, closeDuration).SetEase(closeEase));

        // baseScale を基準に「閉じ終わり倍率」を掛ける
        if (panelRect != null)
            seq.Join(panelRect.DOScale(baseScale * closeToScale, closeDuration).SetEase(closeEase));

        seq.OnComplete(ApplyClosedInstant);

        panelTween = seq.SetLink(creditPanel, LinkBehaviour.KillOnDestroy);
    }

    private void ApplyClosedInstant()
    {
        KillTween();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 閉じた後も「表示上の等倍」＝ baseScale に戻しておく（次回の状態を安定させる）
        if (panelRect != null)
            panelRect.localScale = Vector3.one * baseScale;

        creditPanel.SetActive(false);
        state.Value = PanelState.Closed;
    }

    private void KillTween()
    {
        if (panelTween != null && panelTween.IsActive())
        {
            panelTween.Kill();
            panelTween = null;
        }
    }

    private void OnDestroy()
    {
        KillTween();
    }
}
