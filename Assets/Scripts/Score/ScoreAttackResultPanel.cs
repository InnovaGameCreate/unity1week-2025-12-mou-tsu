using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;

using unityroom.Api;

public class ScoreAttackResultPanel : MonoBehaviour
{
    public static ScoreAttackResultPanel Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text countText;    // 「99<size=35%>つなぎ</size>」形式
    [SerializeField] private TMP_Text rankingText;  // 「99<size=35%>位</size>」形式

    [Header("戻り先")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("unityroom ランキング")]
    [SerializeField, Tooltip("unityroomで作成したスコアボード番号")]
    private int scoreboardNo = 1;

    [Header("unityroom 記録ルール")]
    [SerializeField] private ScoreboardWriteMode writeMode = ScoreboardWriteMode.Always;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // ルートに対して DontDestroyOnLoad を適用（子に付いていてもエラーを出さないように）
        var rootGo = transform.root != null ? transform.root.gameObject : gameObject;
        DontDestroyOnLoad(rootGo);

        if (panelRoot != null) panelRoot.SetActive(false);

        // 方針変更：ランキングテキストは表示しない
        if (rankingText != null) rankingText.gameObject.SetActive(false);
    }

    public void Show(int clearedCount)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        // クリア数を表示
        if (countText != null)
            countText.text = $"{clearedCount}<size=35%>つなぎ</size>";

        // ランキング表記は非表示方針のため、何もしない

        // unityroomにスコア送信
        SendScore(clearedCount);
    }

    private void SendScore(int score)
    {
        // unityroom-client-unity 導入済み時：スコア送信（順位取得はSDKのランキング表示に依存）
        UnityroomApiClient.Instance.SendScore(scoreboardNo, score, writeMode);
        Debug.Log($"[ScoreAttack] unityroomへ送信: score={score}, board={scoreboardNo}, mode={writeMode}");
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ボタンに登録
    public void OnRetry()
    {
        Hide();
        if (ScoreAttackManager.Instance != null)
            ScoreAttackManager.Instance.StartRun();
    }

    // ボタンに登録
    public void OnBackToTitle()
    {
        Hide();
        DestroyUnityroomApiClients();
        // タイトルに戻るときは自分を破棄してクリーンにする（DontDestroyOnLoadで残っているため）
        Destroy(transform.root.gameObject);
        SceneManager.LoadScene(titleSceneName);
    }

    private void DestroyUnityroomApiClients()
    {
        var clients = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(c => c != null && c.GetType().Name == "UnityroomApiClient")
            .Select(c => c.gameObject)
            .Distinct()
            .ToList();

        foreach (var go in clients)
        {
            Object.Destroy(go);
        }
    }
}
