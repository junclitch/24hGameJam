using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 1FixedUpdateのあいだ各 PlayerAction に共有される実行コンテキスト。
    /// PlayerController が毎物理フレーム値を埋めて回し、各アクションは読み書き両方できる。
    /// 物理量や判定結果を持ち回るだけのデータ袋なので、ロジックは持たない。
    /// </summary>
    public class PlayerActionContext
    {
        public Rigidbody2D rb;
        public Transform groundCheck;
        public Collider2D bodyCollider;
        public PlayerStats stats;
        public ActionInventory inventory;

        // 入力 (Update側で集約された結果)
        public float inputX;
        public float facingDir;
        public bool jumpRequested;
        public bool evasionRequested;

        // 接地状態。GroundCheckAction 所持有無に応じて PlayerController が毎フレーム計算
        public bool isGrounded;

        // 着地した瞬間のフレームか (空中→接地に切り替わったフレームで true)
        public bool justLanded;
    }
}
