using UnityEngine;
using TMPro;
using UniRx;
using DG.Tweening;
using System;

public class SamplePressAnyKeyBlink : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TextMeshProUGUI pressAnyKeyText;

    [Header("設定")]
    [SerializeField] private float startDelay = 1.0f;     // 表示開始までの待ち
    [SerializeField] private float blinkInterval = 0.6f;  // 点滅間隔

    private Tween blinkTween;

    private void Start()
    {
        if (pressAnyKeyText == null)
        {
            Debug.LogError("pressAnyKeyText が未設定です");
            return;
        }

        // 最初は見えなくする（Activeは切らない）
        pressAnyKeyText.alpha = 0f;

        // 1秒後に点滅開始
        Observable.Timer(TimeSpan.FromSeconds(startDelay))
            .Subscribe(_ => StartBlink())
            .AddTo(this);
    }

    private void StartBlink()
    {
        // 念のため既存Tweenを止める
        blinkTween?.Kill();

        // alpha を 0 → 1 → 0 … と繰り返す
        blinkTween = pressAnyKeyText
            .DOFade(1f, blinkInterval)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(pressAnyKeyText.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void OnDestroy()
    {
        blinkTween?.Kill();
    }
}
