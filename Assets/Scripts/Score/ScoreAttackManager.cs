using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreAttackManager : MonoBehaviour
{
    public static ScoreAttackManager Instance { get; private set; }

    [Header("設定")]
    [SerializeField] private int timeLimitSeconds = 60;
    [SerializeField] private List<string> stageSceneNames = new List<string>(); // Build Settingsに追加済みのシーン名

    // 外部参照したいならReactivePropertyにしてもいい
    public IReadOnlyReactiveProperty<int> RemainingSeconds => _remainingSeconds;
    public IReadOnlyReactiveProperty<int> ClearedCount => _clearedCount;
    public IReadOnlyReactiveProperty<bool> IsRunning => _isRunning;
    public bool HasPlayedCountdown { get; set; }
    public bool IsResultShown => _resultShown;

    private readonly ReactiveProperty<int> _remainingSeconds = new ReactiveProperty<int>(0);
    private readonly ReactiveProperty<int> _clearedCount = new ReactiveProperty<int>(0);
    private readonly ReactiveProperty<bool> _isRunning = new ReactiveProperty<bool>(false);

    private readonly List<string> _stageOrder = new List<string>();
    private readonly System.Random _rng = new System.Random();

    private int _stageIndex;
    private bool _resultShown;
    private bool _timerStarted;
    private CompositeDisposable _runDisposables = new CompositeDisposable();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        _runDisposables.Dispose();
    }

    // Unityバージョン差を吸収して「非アクティブ含めて最初の1つ」を取る
    private static T FindInactiveSafe<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<T>(true);
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // タイトルに戻ったら状態をリセット（スコアアタックを抜ける）
        if (scene.name == "Title")
        {
            _isRunning.Value = false;
            _runDisposables.Clear();
            _stageOrder.Clear();
            CleanupResultPanel();
            HideTimeHud();
            return;
        }

        // 非実行時：残存パネル/HUDをクリーンアップ
        if (!_isRunning.Value)
        {
            CleanupResultPanel();
            HideTimeHud();
            return;
        }

        // スコアアタック中のみ StageName を無効化
        DisableStageNameObject();

        // 321 カウントダウン完了後にタイマー開始（まだ開始していない場合のみ）
        if (!_timerStarted)
        {
            var presenter = FindInactiveSafe<CountdownPresenter>();
            if (presenter != null)
            {
                presenter.OnCountdownFinished
                    .Take(1)
                    .Subscribe(_ => StartTimerIfNeeded())
                    .AddTo(_runDisposables);
            }
            else
            {
                // 見つからないケース（演出がないシーンなど）は即開始
                StartTimerIfNeeded();
            }
        }
    }

    private void CleanupResultPanel()
    {
        if (ScoreAttackResultPanel.Instance != null)
        {
            Destroy(ScoreAttackResultPanel.Instance.transform.root.gameObject);
        }
    }

    private void HideTimeHud()
    {
        if (ScoreAttackTimeHud.Instance != null)
        {
            ScoreAttackTimeHud.Instance.Hide();
        }
    }

    private void DisableStageNameObject()
    {
        // Canvas/StageName を検索して無効化
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        var stageName = canvas.transform.Find("StageName");
        if (stageName != null)
        {
            stageName.gameObject.SetActive(false);
            Debug.Log("[ScoreAttack] StageName を無効化しました");
        }
    }

    public void StartRun()
    {
        if (stageSceneNames == null || stageSceneNames.Count == 0)
        {
            Debug.LogError("stageSceneNames が空です（ScoreAttackManager）");
            return;
        }

        // 既に走ってたら止めてリスタート
        _runDisposables.Clear();

        _clearedCount.Value = 0;
        _remainingSeconds.Value = timeLimitSeconds;
        BuildRandomStageOrder();
        _isRunning.Value = true;
        _resultShown = false;
        HasPlayedCountdown = false; // 新しいラン開始時はリセット
        _timerStarted = false;

        LoadCurrentStage();
    }

    private void StartTimerIfNeeded()
    {
        if (_timerStarted || !_isRunning.Value) return;
        _timerStarted = true;

        // 1秒ごとに残り時間を減らす（Update不要）
        Observable.Interval(TimeSpan.FromSeconds(1))
            .TakeWhile(_ => _isRunning.Value)
            .Subscribe(_ =>
            {
                var next = _remainingSeconds.Value - 1;
                _remainingSeconds.Value = Mathf.Max(0, next);

                if (_remainingSeconds.Value <= 0)
                {
                    EndRun();
                }
            })
            .AddTo(_runDisposables);
    }

    public void OnStageCleared()
    {
        if (!_isRunning.Value || _resultShown) return;

        _clearedCount.Value++;

        _stageIndex++;
        if (_stageIndex >= _stageOrder.Count)
        {
            // 周回時は再シャッフル
            BuildRandomStageOrder();
            _stageIndex = 0;
        }

        LoadCurrentStage();
    }

    private void LoadCurrentStage()
    {
        if (_stageOrder.Count == 0)
        {
            Debug.LogError("[ScoreAttack] ステージリストが空です");
            return;
        }

        var sceneName = _stageOrder[_stageIndex];
        SceneManager.LoadScene(sceneName);
    }

    private void BuildRandomStageOrder()
    {
        _stageOrder.Clear();
        _stageOrder.AddRange(stageSceneNames);

        // Fisher-Yates シャッフル
        for (int i = _stageOrder.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_stageOrder[i], _stageOrder[j]) = (_stageOrder[j], _stageOrder[i]);
        }

        _stageIndex = 0;
    }

    private void EndRun()
    {
        if (!_isRunning.Value) return;

        _isRunning.Value = false;
        _runDisposables.Clear();
        _resultShown = true;

        EnsureResultPanelExists();

        if (ScoreAttackResultPanel.Instance != null)
            ScoreAttackResultPanel.Instance.Show(_clearedCount.Value);
        else
            Debug.LogWarning("ScoreAttackResultPanel.Instance が null です");
    }

    private void EnsureResultPanelExists()
    {
        if (ScoreAttackResultPanel.Instance != null) return;

        var prefab = Resources.Load<GameObject>("ScoreAttack/ScoreAttackResultPanel");
        if (prefab == null)
        {
            Debug.LogError("Missing prefab: Assets/Resources/ScoreAttack/ScoreAttackResultPanel.prefab");
            return;
        }

        var go = Instantiate(prefab);
        if (!go.activeSelf) go.SetActive(true);

        if (go.GetComponentInChildren<ScoreAttackResultPanel>(true) == null)
            Debug.LogError("ScoreAttackResultPanel.prefab に ScoreAttackResultPanel コンポーネントが付いてない。");
    }
}
