using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 長押し中に一定間隔で線の速度を通常⇔高速に交互に切り替える（パルス制御）。
/// 2秒通常 → 2秒高速 → 2秒通常 → … のように繰り返す。
/// ファクトリ生成分も含め全 DrawLineFromClick に適用する。
/// 特定ステージ専用：このコンポーネントを置かないステージには影響しない。
/// </summary>
public class LineSpeedPulseController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField, Tooltip("最初の DrawLineFromClick。未設定なら近くから自動取得")]
    private DrawLineFromClick drawer;

    [SerializeField, Tooltip("ファクトリ（2本目以降の Drawer 生成を検知）。未設定なら近くから自動取得")]
    private DrawLineSystemFactory drawSystemFactory;

    [Header("パルス設定")]
    [SerializeField, Tooltip("通常フェーズの長さ（秒）")]
    private float normalSeconds = 2f;

    [SerializeField, Tooltip("高速フェーズの長さ（秒）")]
    private float boostSeconds = 2f;

    [SerializeField, Tooltip("高速フェーズ時の速度倍率")]
    private float boostMultiplier = 3f;

    [Header("スプライト演出")]
    [SerializeField, Tooltip("高速フェーズ時に色が変わる SpriteRenderer（最大9個）")]
    private SpriteRenderer[] pulseSprites = new SpriteRenderer[9];

    [SerializeField, Tooltip("高速フェーズ時のスプライト色")]
    private Color boostColor = new Color(1f, 0.5f, 0f, 1f); // オレンジ

    [Header("デバッグ")]
    [SerializeField] private bool enableLog = false;

    // 追跡中の全 Drawer
    private readonly List<DrawLineFromClick> trackedDrawers = new List<DrawLineFromClick>();
    private Color[] originalColors;
    private bool isBoosted;
    private float nextLogTime;

    void Start()
    {
        // Drawer を自動取得
        if (drawer == null) drawer = GetComponent<DrawLineFromClick>();
        if (drawer == null) drawer = GetComponentInChildren<DrawLineFromClick>(true);
        if (drawer == null) drawer = GetComponentInParent<DrawLineFromClick>(true);
        if (drawer != null) TrackDrawer(drawer);

        // Factory を自動取得
        if (drawSystemFactory == null) drawSystemFactory = GetComponent<DrawLineSystemFactory>();
        if (drawSystemFactory == null) drawSystemFactory = GetComponentInChildren<DrawLineSystemFactory>(true);
        if (drawSystemFactory == null) drawSystemFactory = GetComponentInParent<DrawLineSystemFactory>(true);

        if (drawSystemFactory != null)
        {
            drawSystemFactory.OnDrawerCreated += TrackDrawer;
        }

        // スプライトの元の色を保持
        originalColors = new Color[pulseSprites.Length];
        for (int i = 0; i < pulseSprites.Length; i++)
        {
            if (pulseSprites[i] != null)
                originalColors[i] = pulseSprites[i].color;
        }
    }

    void OnDestroy()
    {
        if (drawSystemFactory != null)
        {
            drawSystemFactory.OnDrawerCreated -= TrackDrawer;
        }

        // 速度を戻す
        SetMultiplierAll(1f);
        RestoreSpriteColors();
    }

    private void TrackDrawer(DrawLineFromClick d)
    {
        if (d != null && !trackedDrawers.Contains(d))
        {
            trackedDrawers.Add(d);

            // 既にブースト中なら即反映
            if (isBoosted)
            {
                d.SpeedMultiplier = boostMultiplier;
            }
        }
    }

    void LateUpdate()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        if (normalSeconds <= 0f || boostSeconds <= 0f) return;

        float sceneSeconds = Time.timeSinceLevelLoad;
        float cycleSeconds = normalSeconds + boostSeconds;
        float t = sceneSeconds % cycleSeconds;
        bool shouldBoost = t >= normalSeconds;

        if (shouldBoost != isBoosted)
        {
            SetBoosted(shouldBoost);
            LogThrottled($"SpeedPulse: boost={shouldBoost} scene={sceneSeconds:F2}s");
        }

        bool isPressed = pointer.press.isPressed;
        if (!isPressed)
        {
            SetMultiplierAll(1f);
            return;
        }

        SetMultiplierAll(shouldBoost ? boostMultiplier : 1f);
    }

    private void SetMultiplierAll(float multiplier)
    {
        // null 除去しつつ全 Drawer に適用
        for (int i = trackedDrawers.Count - 1; i >= 0; i--)
        {
            var d = trackedDrawers[i];
            if (d == null)
            {
                trackedDrawers.RemoveAt(i);
                continue;
            }
            d.SpeedMultiplier = multiplier;
        }
    }

    private void SetBoosted(bool boosted)
    {
        isBoosted = boosted;
        if (boosted)
        {
            SetSpriteColors(boostColor);
        }
        else
        {
            RestoreSpriteColors();
        }
    }

    private void SetSpriteColors(Color color)
    {
        for (int i = 0; i < pulseSprites.Length; i++)
        {
            if (pulseSprites[i] != null)
                pulseSprites[i].color = color;
        }
    }

    private void RestoreSpriteColors()
    {
        if (originalColors == null) return;
        for (int i = 0; i < pulseSprites.Length; i++)
        {
            if (pulseSprites[i] != null && i < originalColors.Length)
                pulseSprites[i].color = originalColors[i];
        }
    }

    private void LogThrottled(string message)
    {
        if (!enableLog) return;
        if (Time.unscaledTime < nextLogTime) return;
        nextLogTime = Time.unscaledTime + 0.2f;
        Debug.Log(message);
    }
}
