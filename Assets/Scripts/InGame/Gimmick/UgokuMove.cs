using UnityEngine;
using DG.Tweening;

public class HorizontalPingPong : MonoBehaviour
{
    public enum StartDirection
    {
        Right,
        Left
    }

    [Header("移動対象（未設定ならこのGameObject）")]
    [SerializeField] private Transform target;

    [Header("左右1往復の距離（合計）")]
    [SerializeField] private float moveDistance = 4f;

    [Header("1往復にかかる時間（秒）")]
    [SerializeField] private float duration = 4f;

    [Header("最初に動く方向")]
    [SerializeField] private StartDirection startDirection = StartDirection.Right;

    private Tween tween;
    private Vector3 startPos;

    void Awake()
    {
        if (target == null) target = transform;
    }

    void OnEnable()
    {
        StartTween();
    }

    void OnDisable()
    {
        KillTween();
    }

    void OnDestroy()
    {
        KillTween();
    }

    private void StartTween()
    {
        if (target == null) return;

        startPos = target.position;

        float halfDistance = moveDistance / 2f;
        float halfDuration = duration / 2f;

        // 方向によって符号を変える
        float dir = (startDirection == StartDirection.Right) ? 1f : -1f;

        KillTween();

        tween = target.DOMoveX(startPos.x + halfDistance * dir, halfDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void KillTween()
    {
        if (tween != null && tween.IsActive())
        {
            tween.Kill();
            tween = null;
        }
    }
}
