using UnityEngine;
using UniRx;

public class LineDropOnRelease : MonoBehaviour
{
    [SerializeField] private DrawLineFromClick drawer;
    [SerializeField, Tooltip("生成済みラインをぶら下げる親。未設定ならワールド直下に置く")] private Transform lineParentOverride;

    [Header("物理設定（白だけに適用）")]
    [SerializeField] private float gravityScale = 2.5f;
    [SerializeField] private float linearDamping = 0.2f;
    [SerializeField] private float angularDamping = 0.2f;

    [Header("当たり判定（白だけに付ける）")]
    [SerializeField] private bool addCollider = true;
    [SerializeField] private float colliderThickness = 0.15f;

    [Header("バウンド用マテリアル（任意）")]
    [SerializeField] private PhysicsMaterial2D bounceMaterial;

    void Start()
    {
        // 1) 自分と同じオブジェクトに付いている Drawer を優先
        if (drawer == null)
            drawer = GetComponent<DrawLineFromClick>();

        // 2) 子階層にある場合（同じプレハブ内での相互参照崩れ対策）
        if (drawer == null)
            drawer = GetComponentInChildren<DrawLineFromClick>(true);

        // 3) 親階層にある場合（Prefab構造に依存）
        if (drawer == null)
            drawer = GetComponentInParent<DrawLineFromClick>(true);

        // グローバル検索は行わない（別インスタンスを拾って既存ラインに影響を与えないため）
        if (drawer == null)
        {
            Debug.LogError("drawer が未設定です。DrawLineFromClick を同一プレハブ内でアサインしてください。");
            return;
        }

        drawer.OnPressUpAsObservable
            .Subscribe(x => MakeMainLineFallAndBindShadow(x.line, x.shadow, x.start, x.end, x.shadowOffsetWorld))
            .AddTo(this);
    }

    private void MakeMainLineFallAndBindShadow(LineRenderer main, LineRenderer shadow, Vector3 start, Vector3 end, Vector3 shadowOffsetWorld)
    {
        if (main == null) return;

        // すでに物理が付いてたら何もしない
        if (main.GetComponent<Rigidbody2D>() != null) return;

        start.z = 0f; end.z = 0f;
        var dir = end - start;
        float length = dir.magnitude;
        if (length <= 0.0001f) return;

        Vector3 mid = (start + end) * 0.5f;
        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 本体（白）のTransformを“物理オブジェクト”として使う
        var t = main.transform;
        var parent = lineParentOverride != null ? lineParentOverride : null; // スケール汚染防止のためワールド直下がデフォルト
        t.SetParent(parent, true);
        t.position = mid;
        t.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        // 本体ラインをローカル化（物理に強い）
        main.useWorldSpace = false;
        main.positionCount = 2;
        main.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
        main.SetPosition(1, new Vector3( length * 0.5f, 0f, 0f));

        // ✅ 影は本体の子にして、物理は付けず追従だけさせる
        if (shadow != null)
        {
            // 影に物理が付いてたら剥がす（保険）
            var srb = shadow.GetComponent<Rigidbody2D>();
            if (srb != null) Destroy(srb);

            var scol = shadow.GetComponent<Collider2D>();
            if (scol != null) Destroy(scol);

            // 親子化（この時点でワールド姿勢を維持）
            shadow.transform.SetParent(t, true);

            // 「ワールドでのオフセット」を、親の回転に合わせたローカルオフセットへ変換
            var invRot = Quaternion.Inverse(t.rotation);
            Vector3 offsetLocal = invRot * shadowOffsetWorld;

            shadow.transform.localPosition = offsetLocal;
            shadow.transform.localRotation = Quaternion.identity;
            shadow.transform.localScale = Vector3.one;

            // 影ラインもローカルにして本体と同じ形に（オフセットはTransformで担保）
            shadow.useWorldSpace = false;
            shadow.positionCount = 2;
            shadow.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
            shadow.SetPosition(1, new Vector3( length * 0.5f, 0f, 0f));

            // 描画順：常に本体より後ろ（念押し）
            shadow.sortingLayerID = main.sortingLayerID;
            shadow.sortingOrder = main.sortingOrder - 1;
        }

        // ✅ 本体（白）にだけ物理を付与
        var rb = t.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.gravityScale = gravityScale;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;

        if (addCollider && t.GetComponent<Collider2D>() == null)
        {
            var col = t.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(length, colliderThickness);
            col.offset = Vector2.zero;

            if (bounceMaterial != null)
                col.sharedMaterial = bounceMaterial;
        }

        rb.WakeUp();
    }
}
