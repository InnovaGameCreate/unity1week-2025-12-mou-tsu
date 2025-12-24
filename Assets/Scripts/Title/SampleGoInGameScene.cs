using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniRx;

public class SampleGoInGameScene : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private float inputEnableDelay = 1.0f;
    [SerializeField] private string inGameSceneName = "InGame";

    [Header("参照(UI)")]
    [SerializeField] private Button creditButton;          // 一番手前
    [SerializeField] private Button stageTransitionButton; // 全画面透明ボタン（奥）
    [SerializeField] private CreditToggle creditToggle;

    private readonly BoolReactiveProperty canInput = new BoolReactiveProperty(false);
    private readonly BoolReactiveProperty isTransitioning = new BoolReactiveProperty(false);

    private void Start()
    {
        canInput.Value = false;

        if (creditButton != null) creditButton.interactable = false;
        if (stageTransitionButton != null) stageTransitionButton.interactable = false;

        // 1秒後に入力解禁
        Observable.Timer(TimeSpan.FromSeconds(inputEnableDelay))
            .Subscribe(_ => canInput.Value = true)
            .AddTo(this);

        IObservable<bool> creditOpenObs =
            (creditToggle != null) ? creditToggle.IsOpenRx : Observable.Return(false);

        // ステージ遷移ボタン：入力解禁 && クレジット閉 && 未遷移 のときだけ押せる
        canInput
            .CombineLatest(creditOpenObs, isTransitioning, (can, creditOpen, transitioning) =>
                can && !creditOpen && !transitioning)
            .DistinctUntilChanged()
            .Subscribe(ok =>
            {
                if (stageTransitionButton != null) stageTransitionButton.interactable = ok;
            })
            .AddTo(this);

        // クレジットボタン：入力解禁 && 未遷移 のときだけ押せる
        canInput
            .CombineLatest(isTransitioning, (can, transitioning) => can && !transitioning)
            .DistinctUntilChanged()
            .Subscribe(ok =>
            {
                if (creditButton != null) creditButton.interactable = ok;
            })
            .AddTo(this);

        // クレジットボタン：Toggle
        if (creditButton != null)
        {
            creditButton.OnClickAsObservable()
                .Where(_ => creditButton.interactable)
                // 念のため：一瞬で2回発火しても1回にする保険
                .ThrottleFirst(TimeSpan.FromMilliseconds(150))
                .Subscribe(_ => creditToggle?.Toggle())
                .AddTo(this);
        }

        // 全画面ボタン：遷移（押せる時だけ）
        if (stageTransitionButton != null)
        {
            stageTransitionButton.OnClickAsObservable()
                .Where(_ => stageTransitionButton.interactable)
                .Take(1)
                .Subscribe(_ => GoInGame())
                .AddTo(this);
        }
    }

    private void GoInGame()
    {
        isTransitioning.Value = true;
        canInput.Value = false;
        SceneManager.LoadScene(inGameSceneName);
    }
}
