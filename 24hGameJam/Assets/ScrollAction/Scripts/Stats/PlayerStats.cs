using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// プレイヤーの挙動パラメータ (ScriptableObject)。
    /// 値はすべて .asset 側で設定する。スクリプトに数値を直書きしない方針。
    /// 1つのアセットを複数キャラで共有したり、難易度別アセットを差し替えたりできる。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "ScrollAction/Player Stats")]
    public class PlayerStats : ScriptableObject
    {
        [Header("Movement")]
        // 横移動の最高速度 (units/sec)
        public float walkSpeed;

        // 入力に対する加減速の鋭さ。大きいほどキビキビ動く
        public float acceleration;

        [Header("Jump")]
        // ジャンプ時に与える上向き速度 (units/sec)
        public float jumpForce;

        // 接地判定のサークル半径
        public float groundCheckRadius;

        // 接地と見なすレイヤ
        public LayerMask groundLayer;

        [Header("Evasion")]
        // 回避 (地上/空中共通) で与える水平方向の瞬間速度 (units/sec)
        public float evasionSpeed;

        [Header("Respawn (安全地帯チェックポイント)")]
        // 足元から左右にこの距離だけ離れた点で地面を確認。両方に地面があれば "幅のある安全地帯"
        // と判定し、その位置を死亡時のリスポーン地点として記録する
        public float safeGroundCheckRange;
    }
}
