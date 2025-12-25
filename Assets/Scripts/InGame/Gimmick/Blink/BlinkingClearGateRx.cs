using System;
using UnityEngine;
using UniRx;

/// <summary>
/// Stage7専用：BlinkingGimmickControllerと連携して、表示中のみクリア判定を有効化
/// StickFitJudgeRxのクリア判定をフィルタリングして、非表示中はクリアできないようにする
/// </summary>
public class BlinkingClearGateRx : MonoBehaviour
{
    [Header("参照")]
    [SerializeField, Tooltip("判定元のStickFitJudgeRx")]
    private StickFitJudgeRx judgeRx;

    [SerializeField, Tooltip("表示制御のBlinkingGimmickController")]
    private BlinkingGimmickController blinkingController;

    // 外部に公開するクリアイベント（表示中のみ通過）
    private readonly Subject<Unit> onClearedGated = new Subject<Unit>();
    public IObservable<Unit> OnClearedGatedAsObservable => onClearedGated;

    // 元のイベントも購読可能に
    public IObservable<StickFitJudgeRx.FitProgress> OnProgressAsObservable => judgeRx?.OnProgressAsObservable;
    public IObservable<Unit> OnFailedAsObservable => judgeRx?.OnFailedAsObservable;

    void Start()
    {
        if (judgeRx == null)
        {
            Debug.LogError("[BlinkingClearGateRx] judgeRx が未設定です。");
            return;
        }

        if (blinkingController == null)
        {
            Debug.LogError("[BlinkingClearGateRx] blinkingController が未設定です。");
            return;
        }

        // StickFitJudgeRxのクリアイベントを購読し、表示中のみ通過させる
        judgeRx.OnClearedAsObservable
            .WithLatestFrom(blinkingController.IsVisibleAsObservable, (_, isVisible) => isVisible)
            .Where(isVisible => isVisible) // 表示中のみ
            .Subscribe(_ =>
            {
                Debug.Log("[BlinkingClearGateRx] クリア判定通過（表示中）");
                onClearedGated.OnNext(Unit.Default);
            })
            .AddTo(this);

        // デバッグ用：非表示中にクリア判定が来た場合のログ
        judgeRx.OnClearedAsObservable
            .WithLatestFrom(blinkingController.IsVisibleAsObservable, (_, isVisible) => isVisible)
            .Where(isVisible => !isVisible)
            .Subscribe(_ =>
            {
                Debug.LogWarning("[BlinkingClearGateRx] クリア判定をブロック（非表示中）");
            })
            .AddTo(this);
    }

    void OnDestroy()
    {
        onClearedGated?.Dispose();
    }
}
