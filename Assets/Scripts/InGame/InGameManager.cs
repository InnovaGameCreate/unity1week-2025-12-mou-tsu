using System;
using UnityEngine;
using UniRx;
using UniRx.Triggers;

public class InGameManager : MonoBehaviour
{
    /// <summary>
    /// このステージ開始「直後」を流す（Startのタイミングで1回）
    /// </summary>
    public IObservable<Unit> OnStageStartImmediate => onStageStartImmediate;
    private readonly Subject<Unit> onStageStartImmediate = new Subject<Unit>();

    /// <summary>
    /// このステージ開始「3秒後」を流す（1回）
    /// </summary>
    public IObservable<Unit> OnStageStartAfter3Sec => onStageStartAfter3Sec;
    private readonly Subject<Unit> onStageStartAfter3Sec = new Subject<Unit>();

    private void Start()
    {
        // シーン（ステージ）開始直後
        onStageStartImmediate.OnNext(Unit.Default);

        // 3秒後（Time.timeScale の影響を受ける）
        Observable.Timer(System.TimeSpan.FromSeconds(3))
            .Take(1)
            .Subscribe(_ => onStageStartAfter3Sec.OnNext(Unit.Default))
            .AddTo(this);
    }

    private void OnDestroy()
    {
        // 明示的に閉じたい派ならこれ（なくてもAddToで破棄されるが、Subjectは閉じとくと安全）
        onStageStartImmediate.OnCompleted();
        onStageStartImmediate.Dispose();

        onStageStartAfter3Sec.OnCompleted();
        onStageStartAfter3Sec.Dispose();
    }
}
