using System;
using UnityEngine;
using UniRx;
using TMPro;
using DG.Tweening;

public class CountdownPresenter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private InGameManager inGameManager;

    [Header("TMP")]
    [SerializeField] private TextMeshProUGUI frontText;   // 表示用
    [SerializeField] private TextMeshProUGUI shadowText;  // 影用

    [Header("Timing")]
    [SerializeField] private float stepDuration = 1.0f;
    [SerializeField] private float popDuration  = 0.2f;

    [Header("Scale")]
    [SerializeField] private float startScale = 2.0f;
    [SerializeField] private float endScale   = 1.0f;

    [Header("SE")]
    [SerializeField] private AudioSource countdownSE;

    // ✅ カウントダウン終了を流すObservable
    public IObservable<Unit> OnCountdownFinished => onCountdownFinished;
    private readonly Subject<Unit> onCountdownFinished = new Subject<Unit>();

    private Sequence seq;

    private void Awake()
    {
        SetActive(false);
    }

    private void Start()
    {
        inGameManager.OnStageStartImmediate
            .Delay(TimeSpan.FromSeconds(0.5f))
            .Take(1)
            .Subscribe(_ => PlayCountdown())
            .AddTo(this);
    }

    private void PlayCountdown()
    {
        seq?.Kill();
        seq = DOTween.Sequence().SetLink(gameObject);

        SetActive(true);
        seq.AppendInterval(0f);

        AddStep("3");
        AddStep("2");
        AddStep("1");

        seq.OnComplete(() =>
        {
            SetActive(false);

            // ✅ 終了通知
            onCountdownFinished.OnNext(Unit.Default);
        });
    }

    private void AddStep(string value)
    {
        seq.AppendCallback(() =>
        {
            frontText.text  = value;
            shadowText.text = value;

            ResetTMP(frontText);
            ResetTMP(shadowText);

            // 表示がセットされた直後に鳴らす（ここだけ）
            if (countdownSE != null && countdownSE.clip != null)
                countdownSE.PlayOneShot(countdownSE.clip);
        });

        seq.Append(frontText.DOFade(1f, popDuration));
        seq.Join(shadowText.DOFade(0.5f, popDuration));

        seq.Join(frontText.rectTransform
            .DOScale(endScale, popDuration)
            .SetEase(Ease.OutBack));

        seq.Join(shadowText.rectTransform
            .DOScale(endScale, popDuration)
            .SetEase(Ease.OutBack));

        // ★ここを削除（これが即鳴ってた）
        // if (countdownSE != null)
        // {
        //     countdownSE.PlayOneShot(countdownSE.clip);
        // }

        var hold = Mathf.Max(0f, stepDuration - popDuration);
        seq.AppendInterval(hold);

        seq.Append(frontText.DOFade(0f, 0.12f));
        seq.Join(shadowText.DOFade(0f, 0.12f));
    }


    private void ResetTMP(TextMeshProUGUI tmp)
    {
        tmp.alpha = 0f;
        tmp.rectTransform.localScale = Vector3.one * startScale;
    }

    private void SetActive(bool active)
    {
        if (frontText != null)  frontText.gameObject.SetActive(active);
        if (shadowText != null) shadowText.gameObject.SetActive(active);
    }

    private void OnDestroy()
    {
        seq?.Kill();

        // ✅ Subjectを閉じる
        onCountdownFinished.OnCompleted();
        onCountdownFinished.Dispose();
    }
}
