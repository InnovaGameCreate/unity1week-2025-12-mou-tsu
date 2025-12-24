using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

[RequireComponent(typeof(PolygonCollider2D))]
public class TriggerScaleByTag : MonoBehaviour
{
    [Header("反応するタグ")]
    [SerializeField] private string targetTag = "Player";

    [Header("拡大設定")]
    [SerializeField] private float scaleMultiplier = 1.5f;
    [SerializeField] private float scaleDuration = 0.25f;
    [SerializeField] private Ease scaleEase = Ease.OutQuad;

    [Header("退出時に元へ戻す（不要ならOFF）")]
    [SerializeField] private bool restoreOnExit = true;

    // 元のスケールを保持（対象ごと）
    private readonly Dictionary<Transform, Vector3> initialScales = new Dictionary<Transform, Vector3>();

    // 対象ごとのTweenを保持して二重再生を防ぐ
    private readonly Dictionary<Transform, Tween> tweens = new Dictionary<Transform, Tween>();

    private void Awake()
    {
        var col = GetComponent<PolygonCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(targetTag)) return;
        if (!other.CompareTag(targetTag)) return;

        var t = other.transform;

        // 初期スケールを記録（初回だけ）
        if (!initialScales.ContainsKey(t))
        {
            initialScales[t] = t.localScale;
        }

        // 既存Tweenがあれば止める
        KillTween(t);

        // 拡大
        var targetScale = initialScales[t] * scaleMultiplier;
        tweens[t] = t.DOScale(targetScale, scaleDuration).SetEase(scaleEase);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!restoreOnExit) return;
        if (string.IsNullOrEmpty(targetTag)) return;
        if (!other.CompareTag(targetTag)) return;

        var t = other.transform;

        if (!initialScales.ContainsKey(t)) return;

        KillTween(t);

        // 元のサイズへ戻す
        tweens[t] = t.DOScale(initialScales[t], scaleDuration).SetEase(scaleEase);
    }

    private void KillTween(Transform t)
    {
        if (tweens.TryGetValue(t, out var tw) && tw != null && tw.IsActive())
        {
            tw.Kill();
        }
        tweens.Remove(t);
    }

    private void OnDestroy()
    {
        // 念のため全Tweenを停止
        foreach (var kv in tweens)
        {
            var tw = kv.Value;
            if (tw != null && tw.IsActive()) tw.Kill();
        }
        tweens.Clear();
        initialScales.Clear();
    }
}
