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
        // ↓キー or S キーを「押している間」 true。離した瞬間に false (押下フレーム判定ではない)
        public bool crouchPressed;

        // ジェットパック等「長押し継続」入力。jumpRequested とは別に、押している間 true が立つ
        public bool jetpackHeld;

        // 接地状態。GroundCheckAction 所持有無に応じて PlayerController が毎フレーム計算
        public bool isGrounded;

        // 着地した瞬間のフレームか (空中→接地に切り替わったフレームで true)
        public bool justLanded;

        // しゃがみ実行中フラグ。CrouchAction が条件成立時に true をセットし、
        // PlayerController が Tick 後に読み出して PlayerAnimatorBridge へ橋渡しする
        public bool isCrouching;
    }
}
