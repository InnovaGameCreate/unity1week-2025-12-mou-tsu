using UnityEngine;
using UnityEngine.UI;
using UniRx;
using DG.Tweening;

public class CreditToggle : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private GameObject creditPanel;

    [Header("アニメ設定")]
    [SerializeField] private float openDuration = 0.25f;
    [SerializeField] private float closeDuration = 0.18f;
    [SerializeField] private float openFromScale = 0.96f;
    [SerializeField] private float closeToScale = 0.92f;
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
        // ★ここが重要：アニメ中の連打（または二重発火）を無視する
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

        if (panelRect != null) panelRect.localScale = Vector3.one * openFromScale;

        state.Value = PanelState.Opening;

        var seq = DOTween.Sequence();
        seq.Join(canvasGroup.DOFade(1f, openDuration).SetEase(openEase));
        if (panelRect != null)
            seq.Join(panelRect.DOScale(1f, openDuration).SetEase(openEase));

        seq.OnComplete(() => state.Value = PanelState.Open);
        panelTween = seq;
    }

    public void Close()
    {
        if (creditPanel == null) return;
        if (state.Value == PanelState.Closed || state.Value == PanelState.Closing) return;

        KillTween();

        // 閉じ中も裏を押せない（重要）
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        state.Value = PanelState.Closing;

        var seq = DOTween.Sequence();
        seq.Join(canvasGroup.DOFade(0f, closeDuration).SetEase(closeEase));
        if (panelRect != null)
            seq.Join(panelRect.DOScale(closeToScale, closeDuration).SetEase(closeEase));

        seq.OnComplete(ApplyClosedInstant);
        panelTween = seq;
    }

    private void ApplyClosedInstant()
    {
        KillTween();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (panelRect != null) panelRect.localScale = Vector3.one;

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
