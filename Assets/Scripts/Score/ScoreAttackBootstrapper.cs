using UnityEngine;

public static class ScoreAttackBootstrapper
{
    private const string ManagerPath = "ScoreAttack/ScoreAttackManager";
    private const string ResultPanelPath = "ScoreAttack/ScoreAttackResultPanel";
    private const string TimeHudPath = "ScoreAttack/ScoreAttackTimeHud";

    public static void Start()
    {
        EnsureObjects();

        if (ScoreAttackResultPanel.Instance == null)
        {
            Debug.LogError("ScoreAttackResultPanel.Instance が null。Prefabのパス/名前/アクティブ状態/コンポーネント付与を確認して。");
            return;
        }

        if (ScoreAttackManager.Instance == null)
        {
            Debug.LogError("ScoreAttackManager.Instance が null。Prefabのパス/名前/アクティブ状態/コンポーネント付与を確認して。");
            return;
        }

        ScoreAttackResultPanel.Instance.Hide();
        ScoreAttackManager.Instance.StartRun();
    }

    private static void EnsureObjects()
    {
        // Manager
        if (ScoreAttackManager.Instance == null)
        {
            var prefab = Resources.Load<GameObject>(ManagerPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing prefab: Assets/Resources/{ManagerPath}.prefab");
            }
            else
            {
                var go = Object.Instantiate(prefab);
                if (!go.activeSelf) go.SetActive(true);

                if (go.GetComponentInChildren<ScoreAttackManager>(true) == null)
                    Debug.LogError("ScoreAttackManager.prefab に ScoreAttackManager コンポーネントが付いてない。");
            }
        }

        // ResultPanel
        if (ScoreAttackResultPanel.Instance == null)
        {
            var prefab = Resources.Load<GameObject>(ResultPanelPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing prefab: Assets/Resources/{ResultPanelPath}.prefab");
            }
            else
            {
                var go = Object.Instantiate(prefab);
                if (!go.activeSelf) go.SetActive(true);

                if (go.GetComponentInChildren<ScoreAttackResultPanel>(true) == null)
                    Debug.LogError("ScoreAttackResultPanel.prefab に ScoreAttackResultPanel コンポーネントが付いてない。");
            }
        }

        // TimeHud（シーンをまたいで表示されるタイマー）
        if (ScoreAttackTimeHud.Instance == null)
        {
            var prefab = Resources.Load<GameObject>(TimeHudPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing prefab: Assets/Resources/{TimeHudPath}.prefab");
            }
            else
            {
                var go = Object.Instantiate(prefab);
                if (!go.activeSelf) go.SetActive(true);

                if (go.GetComponentInChildren<ScoreAttackTimeHud>(true) == null)
                    Debug.LogError("ScoreAttackTimeHud.prefab に ScoreAttackTimeHud コンポーネントが付いてない。");
            }
        }
    }
}
