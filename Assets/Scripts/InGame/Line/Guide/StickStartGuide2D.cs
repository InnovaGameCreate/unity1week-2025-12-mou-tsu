using UnityEngine;

/// <summary>
/// 伸ばし棒の「開始位置」ガイド
/// - 赤丸を正解Startpointの真上に自動配置（XはStartに合わせる）
/// - 赤丸中心〜Startpoint を点線(LineRenderer)で描画
/// - 赤丸内で押されたら、開始位置を「赤丸中心」に強制
/// </summary>
public class StickStartGuide2D : MonoBehaviour, IStartPointOverride2D
{
    [Header("参照")]
    [SerializeField] private FitTargetSegment2D targetSegment;

    [Header("赤丸（既存SpriteRenderer）")]
    [SerializeField] private SpriteRenderer startPointMarker;

    [Header("点線(LineRenderer)")]
    [SerializeField] private LineRenderer dottedLine;

    [Header("配置設定")]
    [SerializeField] private float verticalOffset = 2.0f;

    [Header("クリック判定（赤丸の半径：自動）")]
    [SerializeField, Tooltip("赤丸SpriteRendererの見た目から自動計算されます（手入力不要）")]
    private float markerRadius = 0.3f;

    private Vector3 markerCenterWorld;

    void Start()
    {
        if (targetSegment == null || startPointMarker == null || dottedLine == null)
        {
            Debug.LogError("StickStartGuide2D: 参照が未設定");
            enabled = false;
            return;
        }

        SetupMarkerPosition();

        // ✅ 赤丸の半径をSpriteRendererから自動で取得（ワールド単位）
        UpdateMarkerRadiusFromSprite();

        SetupDottedLine();
    }

    private void SetupMarkerPosition()
    {
        Vector2 start = targetSegment.Start;

        markerCenterWorld = new Vector3(
            start.x,
            start.y + verticalOffset,
            startPointMarker.transform.position.z
        );

        startPointMarker.transform.position = markerCenterWorld;
    }

    private void UpdateMarkerRadiusFromSprite()
    {
        // bounds はワールド単位。extents は半サイズ＝半径相当
        // 円のつもりでもスケール/解像度でx,yが微妙に違うことがあるので小さい方を採用
        var ext = startPointMarker.bounds.extents;
        markerRadius = Mathf.Min(ext.x, ext.y);

        // もし何かの理由で0に近いなら保険
        if (markerRadius <= 0.0001f)
        {
            markerRadius = 0.3f;
            Debug.LogWarning("StickStartGuide2D: markerRadiusの自動取得に失敗したためフォールバック値を使用しました。");
        }
    }

    private void SetupDottedLine()
    {
        dottedLine.useWorldSpace = true;
        dottedLine.positionCount = 2;

        // 2DならZ=0に寄せると「見えない」事故が減る
        dottedLine.SetPosition(0, new Vector3(markerCenterWorld.x, markerCenterWorld.y, 0f));
        dottedLine.SetPosition(1, new Vector3(targetSegment.Start.x, targetSegment.Start.y, 0f));
    }

    public bool TryOverrideStartPoint(Vector3 rawPressWorld, out Vector3 startWorld)
    {
        float dist = Vector2.Distance(rawPressWorld, markerCenterWorld);
        if (dist <= markerRadius)
        {
            startWorld = markerCenterWorld;
            startWorld.z = 0f;
            return true;
        }

        startWorld = default;
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(markerCenterWorld, markerRadius);
    }
#endif
}
