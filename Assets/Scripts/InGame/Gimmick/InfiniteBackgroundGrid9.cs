using UnityEngine;

public class InfiniteBackgroundGrid9 : MonoBehaviour
{
    [Header("追従対象（カメラ or プレイヤー）")]
    [Tooltip("このTransformの移動に合わせて背景をループさせます。未設定ならMainCameraを自動使用します。")]
    public Transform 追従対象;

    [Header("背景タイル（合計9枚）")]
    [Tooltip("同じスプライトの背景タイルを9枚登録してください。順番はバラバラでもOK（内部で整列します）。")]
    public Transform[] タイル = new Transform[9];

    [Header("初期整列（推奨ON）")]
    [Tooltip("開始時に、タイルを3×3の格子状にピッタリ整列します。手配置が雑でも揃います。")]
    public bool 開始時に自動整列 = true;

    [Header("隙間/重なり微調整")]
    [Tooltip("タイル間に微小な隙間が見える/重なる場合に調整（通常は0でOK）。単位はワールド座標。")]
    public float ピッチ補正 = 0f;

    // 1枚のタイルのワールド上の幅・高さ（scale込み）
    float タイル幅;
    float タイル高;

    // 3×3の論理グリッドとして扱うための中心座標（追従対象の近くに保つ）
    // 中心セルの「論理的な位置」をこの値で管理する
    Vector2 中心セル座標;

    void Awake()
    {
        if (追従対象 == null)
        {
            if (Camera.main != null) 追従対象 = Camera.main.transform;
        }

        if (タイル == null || タイル.Length != 9)
        {
            Debug.LogError("タイル配列は必ず9要素にしてください。");
            enabled = false;
            return;
        }

        // タイルサイズをSpriteRendererのboundsから取得（scale=0.25なども反映される）
        var sr = タイル[0].GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError("タイル[0]にSpriteRendererが必要です。");
            enabled = false;
            return;
        }

        タイル幅 = sr.bounds.size.x + ピッチ補正;
        タイル高 = sr.bounds.size.y + ピッチ補正;

        if (タイル幅 <= 0.0001f || タイル高 <= 0.0001f)
        {
            Debug.LogError("タイル幅/高さが不正です。Sprite/Renderer設定を確認してください。");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // 初期の中心セル座標を、追従対象に最も近いタイルの位置から決める
        //（雑配置でも、中心を追従対象付近に寄せる）
        Vector2 近い = タイル[0].position;
        if (追従対象 != null)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < 9; i++)
            {
                float d = (new Vector2(タイル[i].position.x, タイル[i].position.y) -
                           new Vector2(追従対象.position.x, 追従対象.position.y)).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    近い = タイル[i].position;
                }
            }
        }

        // 近いタイル位置を「中心セル座標」として採用
        中心セル座標 = 近い;

        if (開始時に自動整列)
        {
            // 3×3の格子に強制整列（中央を中心セル座標に置く）
            再配置_3x3(中心セル座標);
        }
    }

    void Update()
    {
        if (追従対象 == null) return;

        // 追従対象が中心セルからどれだけ離れたかを見る
        float dx = 追従対象.position.x - 中心セル座標.x;
        float dy = 追従対象.position.y - 中心セル座標.y;

        bool moved = false;

        // 追従対象が中心セルから「タイル幅」以上離れたら、
        // 中心セル座標をタイル幅ぶんシフトして3×3全体を追従対象の周りに引き戻す
        if (dx > タイル幅) { 中心セル座標.x += タイル幅; moved = true; }
        else if (dx < -タイル幅) { 中心セル座標.x -= タイル幅; moved = true; }

        if (dy > タイル高) { 中心セル座標.y += タイル高; moved = true; }
        else if (dy < -タイル高) { 中心セル座標.y -= タイル高; moved = true; }

        if (moved)
        {
            // 中心セル座標を更新したら、9枚を3×3に再配置する
            // これにより、どの方向に動いても常に周囲にタイルが存在し続ける
            再配置_3x3(中心セル座標);
        }
    }

    /// <summary>
    /// 9枚のタイルを「中心セル座標」を中心に3×3へ並べ直す。
    /// ※タイルの順番はどうでもよい（見た目は同じ画像のため）。
    /// </summary>
    void 再配置_3x3(Vector2 center)
    {
        // 3×3の座標オフセット（左下→右上の順に埋める）
        // (-1,-1) (0,-1) (1,-1)
        // (-1, 0) (0, 0) (1, 0)
        // (-1, 1) (0, 1) (1, 1)
        int idx = 0;
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                // 画像のZは維持（パララックスなどでZを分けたい場合に壊さない）
                Vector3 p = タイル[idx].position;
                p.x = center.x + ox * タイル幅;
                p.y = center.y + oy * タイル高;
                タイル[idx].position = p;

                idx++;
            }
        }
    }
}
