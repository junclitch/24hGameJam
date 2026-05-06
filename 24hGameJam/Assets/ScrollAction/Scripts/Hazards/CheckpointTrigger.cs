using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーが触れたら自身の位置 (+spawnOffset) をリスポーン地点として登録するチェックポイント。
    /// 一度通過したら以降は反応しない (戻り通過で勝手に上書きしないため)。
    /// 視覚マーカーは別途 SpriteRenderer/MeshRenderer で配置する想定 (このコンポーネントは判定のみ)。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CheckpointTrigger : MonoBehaviour
    {
        [Header("リスポーン位置オフセット (このオブジェクト中心からの相対座標)")]
        // 旗の根元など、見た目の中心と "プレイヤーが立つ位置" がズレている場合の補正
        [SerializeField] private Vector2 spawnOffset;

        // 一度通過したか。再侵入で先に進んだ後の checkpoint を上書きしないためのガード
        private bool reached;

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            Vector3 spawn = transform.position + (Vector3)spawnOffset;
            player.SetRespawnPoint(spawn);
            reached = true;
        }

        /// <summary>Scene View でのみ表示される開発者向けギズモ。Game View には出ない (= 透明)。</summary>
        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Gizmos.color = new Color(0f, 1f, 0.3f, 0.25f);
                Vector3 center = transform.position + (Vector3)box.offset;
                Gizmos.DrawCube(center, new Vector3(box.size.x, box.size.y, 0.1f));

                Gizmos.color = new Color(0f, 1f, 0.3f, 0.9f);
                Gizmos.DrawWireCube(center, new Vector3(box.size.x, box.size.y, 0.1f));
            }

            // リスポーン位置 (spawnOffset 適用後) を黄色球で示す
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + (Vector3)spawnOffset, 0.25f);
        }
    }
}
