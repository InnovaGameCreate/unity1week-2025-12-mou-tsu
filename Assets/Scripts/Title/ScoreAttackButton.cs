using System;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class ScoreAttackButton : MonoBehaviour
{
    [Header("参照(UI)")]
    [SerializeField] private Button scoreAttackButton;
    [SerializeField] private CreditToggle creditToggle;

    [Header("入力制御")]
    [SerializeField] private float inputEnableDelay = 1.0f;

    private readonly BoolReactiveProperty canInput = new BoolReactiveProperty(false);
    private readonly BoolReactiveProperty isTransitioning = new BoolReactiveProperty(false);

    private void Reset()
    {
        scoreAttackButton = GetComponent<Button>();
    }

    private void Start()
    {
        canInput.Value = false;

        if (scoreAttackButton != null)
            scoreAttackButton.interactable = false;

        // 1秒後に入力解禁
        Observable.Timer(TimeSpan.FromSeconds(inputEnableDelay))
            .Subscribe(_ => canInput.Value = true)
            .AddTo(this);

        // クレジット開閉（CreditToggleが無いなら常に閉扱い）
        IObservable<bool> creditOpenObs =
            (creditToggle != null) ? creditToggle.IsOpenRx : Observable.Return(false);

        // ScoreAttackボタン：入力解禁 && クレジット閉 && 未遷移 のときだけ押せる
        canInput
            .CombineLatest(creditOpenObs, isTransitioning,
                (can, creditOpen, transitioning) => can && !creditOpen && !transitioning)
            .DistinctUntilChanged()
            .Subscribe(ok =>
            {
                if (scoreAttackButton != null) scoreAttackButton.interactable = ok;
            })
            .AddTo(this);

        // ScoreAttackボタン：開始（押せる時だけ）
        if (scoreAttackButton != null)
        {
            scoreAttackButton.OnClickAsObservable()
                .Where(_ => scoreAttackButton.interactable)
                .ThrottleFirst(TimeSpan.FromMilliseconds(150))
                .Take(1) // 二重開始防止（開始後は遷移扱い）
                .Subscribe(_ => StartScoreAttack())
                .AddTo(this);
        }
    }

    public void StartScoreAttack()
    {
        isTransitioning.Value = true;
        canInput.Value = false;

        // ここでスコアアタック開始（あなたのBootstrapper）
        ScoreAttackBootstrapper.Start();
    }
}
