using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// パドルの挙動パラメータ (ScriptableObject)。
    /// minX/maxX は壁の内側面からパドル半幅を引いた値にしておく必要がある。
    /// （壁の外にはみ出ないようにするための制約）
    /// </summary>
    [CreateAssetMenu(fileName = "PaddleStats", menuName = "MCPTest/Paddle Stats")]
    public class PaddleStats : ScriptableObject
    {
        // 横移動速度 (units/sec)
        public float speed = 12f;

        // パドル中心の X 座標下限／上限
        public float minX = -6.85f;
        public float maxX = 6.85f;
    }
}
