using UnityEngine;
using UniRx;
using DG.Tweening;

public class ModeMenuUp : MonoBehaviour
{
    [Header("購読元")]
    [SerializeField] private StartButton startButton;

    [Header("動かす親（未指定ならこのGameObjectのTransform）")]
    [SerializeField] private Transform targetRoot;

    [Header("出てくる演出")]
    [SerializeField, Tooltip("開始時に下へどれだけずらすか（ローカル座標）")]
    private float startOffsetY = -80f;

    [SerializeField, Tooltip("アニメ時間（秒）")]
    private float duration = 0.6f;

    [SerializeField] private Ease ease = Ease.OutCubic;

    [SerializeField, Tooltip("開始時に下へずらして待機する")]
    private bool hideOnStart = true;

    private Vector3 baseLocalPos;
    private Tween moveTween;

    private void Awake()
    {
        if (targetRoot == null) targetRoot = transform;   // ★親を動かす
        baseLocalPos = targetRoot.localPosition;

        if (hideOnStart)
        {
            targetRoot.localPosition = baseLocalPos + new Vector3(0f, startOffsetY, 0f);
        }
    }

    private void Start()
    {
        if (startButton == null)
        {
            Debug.LogError("[ModeMenuUp] startButton が未設定です");
            return;
        }

        Debug.Log("[ModeMenuUp] startButton が設定されました。購読開始");

        startButton.OnStartAsObservable
            .Take(1)
            .Subscribe(_ => Play())
            .AddTo(this);
    }

    public void Play()
    {
        Debug.Log("[ModeMenuUp] Play() 開始。targetRoot.localPosition: " + targetRoot.localPosition);
        moveTween?.Kill();

        moveTween = targetRoot
            .DOLocalMove(baseLocalPos, duration)
            .SetEase(ease)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        Debug.Log("[ModeMenuUp] DOLocalMove 実行完了。目標位置: " + baseLocalPos);
    }

    private void OnDestroy()
    {
        moveTween?.Kill();
    }
}
