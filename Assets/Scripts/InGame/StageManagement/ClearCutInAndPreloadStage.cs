using System;
using UnityEngine;
using UniRx;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class ClearCutInAndPreloadStage : MonoBehaviour
{
    [Header("Clear source")]
    [SerializeField] private StickFitJudgeRx judge;

    [Header("Next scene name (must be in Build Settings)")]
    [SerializeField] private string nextSceneName = "Stage2";

    [Header("Cut-in root (Canvas root recommended)")]
    [Tooltip("DontDestroyOnLoad したいルート。未設定ならこのgameObjectを使う")]
    [SerializeField] private GameObject persistRoot;

    [Header("Cut-in UI (RectTransform)")]
    [SerializeField] private RectTransform cutInRect;

    [Header("Wait after clear (seconds)")]
    [SerializeField] private float waitSeconds = 2.5f;

    [Header("Cut-in animation")]
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private float startX = -1400f;
    [SerializeField] private float endX = 1400f;
    [SerializeField] private float y = 0f;

    [Header("Loading")]
    [SerializeField] private bool startLoadAtMid = true;
    [SerializeField] private bool autoActivateWhenReady = true;

    private AsyncOperation preloadOp;
    private bool played;
    private bool loadStarted;
    private bool activated;

    private Sequence seq;

    void Awake()
    {
        if (persistRoot == null) persistRoot = gameObject;
        if (cutInRect == null) cutInRect = GetComponentInChildren<RectTransform>(true);
    }

    void Start()
    {
        if (judge == null)
        {
            Debug.LogError("ClearCutInAndPreloadStage: judge is not assigned.");
            return;
        }
        if (cutInRect == null)
        {
            Debug.LogError("ClearCutInAndPreloadStage: cutInRect is not assigned.");
            return;
        }

        judge.OnProgressAsObservable
            .Where(p => p.cleared)
            .Take(1)
            .Delay(TimeSpan.FromSeconds(waitSeconds))
            .Subscribe(_ => PlayCutInAndSwitch())
            .AddTo(this);
    }

    private void PlayCutInAndSwitch()
    {
        if (played) return;
        played = true;

        MakeRootAndDontDestroy(persistRoot);

        cutInRect.DOKill();
        if (seq != null && seq.IsActive()) seq.Kill();

        cutInRect.anchoredPosition = new Vector2(startX, y);

        float half = duration * 0.5f;

        seq = DOTween.Sequence();
        seq.SetLink(persistRoot, LinkBehaviour.KillOnDestroy);

        seq.Append(cutInRect.DOAnchorPos(new Vector2(0f, y), half).SetEase(ease));

        seq.AppendCallback(() =>
        {
            if (startLoadAtMid) StartPreload();
            if (autoActivateWhenReady) ActivateWhenReady();
        });

        seq.Append(cutInRect.DOAnchorPos(new Vector2(endX, y), half).SetEase(ease));

        seq.OnComplete(() =>
        {
            SafeKillTweens();
            Destroy(persistRoot);
        });
    }

    private void MakeRootAndDontDestroy(GameObject root)
    {
        if (root == null) return;

        // ルート化してからDontDestroy
        if (root.transform.parent != null)
            root.transform.SetParent(null, true);

        DontDestroyOnLoad(root);
    }

    private void StartPreload()
    {
        if (loadStarted) return;
        loadStarted = true;

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("ClearCutInAndPreloadStage: nextSceneName is empty.");
            return;
        }

        preloadOp = SceneManager.LoadSceneAsync(nextSceneName);
        if (preloadOp == null)
        {
            Debug.LogError($"ClearCutInAndPreloadStage: failed to start loading scene '{nextSceneName}'. Check Build Settings.");
            return;
        }

        preloadOp.allowSceneActivation = false;
    }

    private void ActivateWhenReady()
    {
        if (activated) return;

        if (preloadOp == null) StartPreload();
        if (preloadOp == null)
        {
            SceneManager.LoadScene(nextSceneName);
            activated = true;
            return;
        }

        Observable.EveryUpdate()
            .Where(_ => !activated && preloadOp != null && preloadOp.progress >= 0.9f)
            .Take(1)
            .Subscribe(_ =>
            {
                if (activated) return;
                preloadOp.allowSceneActivation = true;
                activated = true;
            })
            .AddTo(this);
    }

    private void SafeKillTweens()
    {
        if (seq != null && seq.IsActive()) seq.Kill();
        if (cutInRect != null) cutInRect.DOKill();
    }

    void OnDestroy()
    {
        SafeKillTweens();
    }
}
