using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class SampleTitleLogoAnim : MonoBehaviour
{
    [SerializeField] float fadeTime = 1.5f;
    [SerializeField] float delay = 0.5f;

    void Start()
    {
        var canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        canvasGroup
            .DOFade(1f, fadeTime)
            .SetDelay(delay)
            .SetEase(Ease.OutQuad);
    }
}
