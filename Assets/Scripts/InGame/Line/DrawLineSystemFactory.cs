using UnityEngine;

/// <summary>
/// DrawLineFromClick を生成・初期化するファクトリ。
/// 2本目以降の描画を委譲することで、インスタンス間の参照汚染を防ぎ、過去のラインへの副作用を遮断する。
/// </summary>
public class DrawLineSystemFactory : MonoBehaviour
{
    [SerializeField, Tooltip("DrawSystemプレハブ (DrawLineFromClick を含むルート)" )]
    private GameObject drawSystemPrefab;

    [SerializeField, Tooltip("生成したインスタンスをぶら下げる親。未設定なら呼び出し元の親を利用" )]
    private Transform parentForInstances;

    /// <summary>
    /// DrawLineFromClick インスタンスを生成し、必須参照を補完した上で描画開始する。
    /// </summary>
    public DrawLineFromClick CreateDrawer(Vector3 startPos, DrawLineFromClick source)
    {
        if (drawSystemPrefab == null)
        {
            Debug.LogError("DrawLineSystemFactory に drawSystemPrefab が設定されていません。");
            return null;
        }
        if (source == null)
        {
            Debug.LogError("CreateDrawer の source が null です。");
            return null;
        }

        var parent = parentForInstances != null ? parentForInstances : source.transform.parent;
        var go = Instantiate(drawSystemPrefab, parent);
        var drawer = go.GetComponent<DrawLineFromClick>();
        if (drawer == null)
        {
            Debug.LogError("drawSystemPrefab に DrawLineFromClick コンポーネントが含まれていません。生成を中止します。");
            Destroy(go);
            return null;
        }

        drawer.CopyMissingReferencesFrom(source);
        drawer.BeginDrawAt(startPos);
        return drawer;
    }
}
