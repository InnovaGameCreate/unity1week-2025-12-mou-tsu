using UnityEngine;
using TMPro;
using UniRx;
using DG.Tweening;
using System;

public class SamplePressAnyKeyBlink : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TextMeshProUGUI pressAnyKeyText;
    [SerializeField] private StartButton startButton;

    [Header("設定")]
    [SerializeField] private float startDelay = 1.0f;     // 表示開始までの待ち
    [SerializeField] private float blinkInterval = 0.6f;  // 点滅間隔

    private Tween blinkTween;
    private IDisposable startDelayDisposable;
    private bool stopped;

    private void Start()
    {
        if (pressAnyKeyText == null)
        {
            Debug.LogError("pressAnyKeyText が未設定です");
            return;
        }

        // 最初は完全に透明
        pressAnyKeyText.alpha = 0f;

        // 一定時間後に点滅開始
        startDelayDisposable =
            Observable.Timer(TimeSpan.FromSeconds(startDelay))
                .Subscribe(_ =>
                {
                    if (!stopped)
                        StartBlink();
                })
                .AddTo(this);

        // Startボタンが押されたらBlink停止＋完全非表示
        if (startButton != null)
        {
            startButton.OnStartAsObservable
                .Take(1)
                .Subscribe(_ => StopBlinkAndHide())
                .AddTo(this);
        }
    }

    private void StartBlink()
    {
        blinkTween?.Kill();

        blinkTween = pressAnyKeyText
            .DOFade(1f, blinkInterval)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(pressAnyKeyText.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void StopBlinkAndHide()
    {
        stopped = true;

        // 点滅開始待ちを停止
        startDelayDisposable?.Dispose();

        // 点滅Tween停止
        blinkTween?.Kill();
        blinkTween = null;

        // 完全に透明に固定
        pressAnyKeyText.alpha = 0f;
    }

    private void OnDestroy()
    {
        blinkTween?.Kill();
        startDelayDisposable?.Dispose();
    }
}
