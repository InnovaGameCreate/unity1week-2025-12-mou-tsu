using UnityEngine;

/// <summary>
/// 正解の型（スロット）を「2点（Start/End）で定義する線分」として扱う。
/// - SpriteRenderer（正解の灰色形）から自動で Start/End を算出
/// - クリア時だけ有効化する SnapCollider（未設定なら自動生成）も管理
/// </summary>
public class FitTargetSegment2D : MonoBehaviour
{
    [Header("正解の形（未指定なら同ObjectのSpriteRendererを自動取得）")]
    [SerializeField] private SpriteRenderer targetSprite;

    [Header("線分方向の追加回転（ローカルZ）")]
    [SerializeField] private float localAngleDegrees = 0f;

    [Header("クリア時にだけ有効化する当たり判定（未指定なら自動生成）")]
    [SerializeField] private Collider2D snapCollider;
    [SerializeField] private bool autoCreateSnapCollider = true;

    [Tooltip("正解形の中心から、ワールド下方向にどれだけズラすか")]
    [SerializeField] private float snapColliderYOffset = -0.02f;

    [SerializeField] private float snapColliderThicknessMultiplier = 1.2f;

    private Vector2 cachedStart;
    private Vector2 cachedEnd;
    private float cachedThickness;

    public Vector2 Start => cachedStart;
    public Vector2 End => cachedEnd;
    public float Thickness => cachedThickness;
    public Collider2D SnapCollider => snapCollider;

    public bool IsValid => targetSprite != null && targetSprite.sprite != null;

    public void EnableSnapCollider()
    {
        if (snapCollider != null) snapCollider.enabled = true;
    }

    public void DisableSnapCollider()
    {
        if (snapCollider != null) snapCollider.enabled = false;
    }

    void Awake()
    {
        if (targetSprite == null) targetSprite = GetComponent<SpriteRenderer>();
        EnsureSnapCollider();
        DisableSnapCollider();
        RecalculateEndpoints();
        SyncSnapCollider();
    }

    void OnValidate()
    {
        if (targetSprite == null) targetSprite = GetComponent<SpriteRenderer>();
        EnsureSnapCollider();
        RecalculateEndpoints();
        SyncSnapCollider();
    }

    private void EnsureSnapCollider()
    {
        if (snapCollider != null) return;
        if (!autoCreateSnapCollider) return;

        Transform child = transform.Find("SnapCollider");
        GameObject go = child != null ? child.gameObject : new GameObject("SnapCollider");
        go.transform.SetParent(transform, false);

        var box = go.GetComponent<BoxCollider2D>();
        if (box == null) box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = false;

        snapCollider = box;
    }

    /// <summary>
    /// spriteのローカルbounds + transform から、ワールド線分の端点を安定算出
    /// </summary>
    public void RecalculateEndpoints()
    {
        if (!IsValid)
        {
            cachedStart = Vector2.zero;
            cachedEnd = Vector2.zero;
            cachedThickness = 0f;
            return;
        }

        Bounds localBounds = targetSprite.sprite.bounds;

        Vector3 worldCenter = targetSprite.transform.TransformPoint(localBounds.center);

        Vector3 s = targetSprite.transform.lossyScale;
        float halfX = Mathf.Abs(localBounds.extents.x * s.x);
        float halfY = Mathf.Abs(localBounds.extents.y * s.y);

        Vector2 baseDir = halfX >= halfY ? (Vector2)targetSprite.transform.right : (Vector2)targetSprite.transform.up;
        float halfLen = Mathf.Max(halfX, halfY);

        float halfThick = Mathf.Min(halfX, halfY);
        cachedThickness = Mathf.Max(halfThick * 2f, 0.0001f);

        Vector2 dir = baseDir.normalized;
        if (Mathf.Abs(localAngleDegrees) > 0.0001f)
            dir = (Quaternion.AngleAxis(localAngleDegrees, Vector3.forward) * dir).normalized;

        cachedStart = (Vector2)worldCenter - dir * halfLen;
        cachedEnd = (Vector2)worldCenter + dir * halfLen;
    }

    private void SyncSnapCollider()
    {
        if (snapCollider == null) return;
        if (!IsValid) return;

        Vector2 center = GetCenter();
        float len = GetLength();
        float ang = GetAngleDegrees();

        var t = snapCollider.transform;
        t.position = new Vector3(center.x, center.y + snapColliderYOffset, t.position.z);
        t.rotation = Quaternion.Euler(0f, 0f, ang);

        if (snapCollider is BoxCollider2D box)
        {
            float thickness = Mathf.Max(Thickness * snapColliderThicknessMultiplier, 0.01f);
            box.size = new Vector2(len, thickness);
            box.offset = Vector2.zero;
        }
    }

    public Vector2 GetCenter()
    {
        return (cachedStart + cachedEnd) * 0.5f;
    }

    public float GetLength()
    {
        return Vector2.Distance(cachedStart, cachedEnd);
    }

    public float GetAngleDegrees()
    {
        Vector2 d = cachedEnd - cachedStart;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        RecalculateEndpoints();
        if (!IsValid) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(cachedStart, cachedEnd);
        Gizmos.DrawSphere(cachedStart, 0.03f);
        Gizmos.DrawSphere(cachedEnd, 0.03f);
    }
#endif
}
