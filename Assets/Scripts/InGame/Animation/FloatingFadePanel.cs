using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class FloatingFadePanel : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float maxAlpha = 0.7f;
    [SerializeField] private float floatUpY = 80f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 basePos;
    private Tween currentTween;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        basePos = rectTransform.anchoredPosition;

        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// ボタンから呼ぶ：表示中ならOFF、非表示ならON
    /// </summary>
    public void Toggle()
    {
        bool isVisible = canvasGroup.alpha > 0.01f;

        currentTween?.Kill();

        if (isVisible)
            Hide();
        else
            Show();
    }

    private void Show()
    {
        rectTransform.anchoredPosition = basePos;

        currentTween = DOTween.Sequence()
            .Join(canvasGroup.DOFade(maxAlpha, fadeDuration)
                .SetEase(Ease.OutCubic))
            .Join(rectTransform.DOAnchorPosY(basePos.y + floatUpY, fadeDuration)
                .SetEase(Ease.OutCubic));
    }

    private void Hide()
    {
        currentTween = DOTween.Sequence()
            .Join(canvasGroup.DOFade(0f, fadeDuration)
                .SetEase(Ease.InCubic))
            .Join(rectTransform.DOAnchorPosY(basePos.y, fadeDuration)
                .SetEase(Ease.InCubic));
    }
}
