using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// パドルの挙動パラメータ (ScriptableObject)。
    /// 値はすべて .asset 側で設定する。
    /// minX/maxX は壁の内側面からパドル半幅を引いた値にしておく必要がある。
    /// （壁の外にはみ出ないようにするための制約）
    /// </summary>
    [CreateAssetMenu(fileName = "PaddleStats", menuName = "MCPTest/Paddle Stats")]
    public class PaddleStats : ScriptableObject
    {
        // 横移動速度 (units/sec)
        public float speed;

        // パドル中心の X 座標下限／上限
        public float minX;
        public float maxX;
    }
}
