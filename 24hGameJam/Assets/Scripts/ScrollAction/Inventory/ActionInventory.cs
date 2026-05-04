using System;
using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーが所持しているアクション能力のフラグを保持する ScriptableObject。
    /// シーンを跨いで状態を保持するため、データはアセット側に置き、コードでは構造のみ宣言する。
    /// 売買で値が書き換わると OnInventoryChanged を発火し、購読側 (Shop UI など) が再描画する。
    /// </summary>
    [CreateAssetMenu(fileName = "ActionInventory", menuName = "ScrollAction/Action Inventory")]
    public class ActionInventory : ScriptableObject
    {
        [Header("基本アクション (初期で所持している想定)")]
        // 左右移動: A/D・左右矢印で水平方向に動けるか
        public bool hasHorizontalMove;

        // 加減速: 入力に応じて速度を Lerp/MoveTowards するか。falseなら目標速度に即セット
        public bool hasAccelDecel;

        // ジャンプ: 接地時にジャンプできる基本能力 (1回分)
        public bool hasJump;

        // 接地判定: 足元の判定が機能するか。falseなら IsGrounded は常に false 扱い
        public bool hasGroundCheck;

        [Header("購入可能アクション")]
        // ジャンプ+1: 総ジャンプ可能回数を +1 する。hasJumpとは独立 (両方持てば2段ジャンプ)
        public bool hasDoubleJump;

        // ダッシュ: 左Shiftで現在の入力方向に瞬間加速。hasHorizontalMoveが無くても効く
        public bool hasDash;

        /// <summary>
        /// 所持状態が変化した時に発火。Shop UI や PlayerController に通知する用途。
        /// アセット書き換えでは Unity が自動で発火しないため、変更後は NotifyChanged() を明示呼び出しすること。
        /// </summary>
        public event Action OnInventoryChanged;

        /// <summary>所持状態を変えた後にこれを呼び、購読側へ通知する。</summary>
        public void NotifyChanged() => OnInventoryChanged?.Invoke();
    }
}
