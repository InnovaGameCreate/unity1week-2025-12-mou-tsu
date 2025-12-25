using UnityEngine;
using UniRx;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(Button))]
public class StartButton : MonoBehaviour
{
    // 外部に流す Observable
    private readonly Subject<Unit> onStart = new Subject<Unit>();
    public IObservable<Unit> OnStartAsObservable => onStart;

    private Button button;
    private bool fired; // 二重発火防止

    private void Awake()
    {
        button = GetComponent<Button>();

        // ButtonクリックをUniRxに変換
        button.OnClickAsObservable()
            .Where(_ => !fired)        // 念のため二重防止
            .Subscribe(_ =>
            {
                fired = true;

                // ① Observable を流す
                onStart.OnNext(Unit.Default);

                // ② 自分自身の Button を無効化
                button.interactable = false;
            })
            .AddTo(this);
    }

    private void OnDestroy()
    {
        onStart.OnCompleted();
    }
}
