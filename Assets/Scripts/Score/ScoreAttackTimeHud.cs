using UniRx;
using TMPro;
using UnityEngine;

/// <summary>
/// スコアアタック用のタイマーHUD
/// シーンをまたいで保持され、スコアアタック中のみ表示
/// Resources/ScoreAttack/ScoreAttackTimeHud.prefab として配置
/// </summary>
public class ScoreAttackTimeHud : MonoBehaviour
{
    public static ScoreAttackTimeHud Instance { get; private set; }

    [SerializeField] private TMP_Text timeText;
    [SerializeField] private GameObject hudRoot; // 表示/非表示を切り替えるルート

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初期は非表示
        SetVisible(false);
    }

    private void Start()
    {
        if (ScoreAttackManager.Instance == null) return;

        // 残り時間を購読
        ScoreAttackManager.Instance.RemainingSeconds
            .Subscribe(sec => UpdateTimeText(sec))
            .AddTo(this);

        // 実行中かどうかで表示/非表示を切り替え
        ScoreAttackManager.Instance.IsRunning
            .Subscribe(running => SetVisible(running))
            .AddTo(this);
    }

    private void UpdateTimeText(int sec)
    {
        if (timeText != null)
            timeText.text = $"{sec}";
    }

    private void SetVisible(bool visible)
    {
        if (hudRoot != null)
            hudRoot.SetActive(visible);
        else
            gameObject.SetActive(visible);
    }

    public void Hide()
    {
        SetVisible(false);
    }
}
