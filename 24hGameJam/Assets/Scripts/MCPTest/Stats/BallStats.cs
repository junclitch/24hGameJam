using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// ボールの挙動パラメータをまとめたデータ定義 (ScriptableObject)。
    /// コードと数値を分離するため、調整はインスペクタ上のアセットで行う。
    /// 同じ BallStats を複数のボールで共有することもできる。
    /// </summary>
    [CreateAssetMenu(fileName = "BallStats", menuName = "MCPTest/Ball Stats")]
    public class BallStats : ScriptableObject
    {
        // ボールの常時保持速度（大きさ）
        public float speed = 8f;

        // この Y 座標を下回ったらボール落下とみなしリセット
        public float resetY = -6f;

        // パドル中央からの距離に応じて反射角を最大何度ふるか（鉛直からの角度）
        public float maxPaddleAngleDeg = 60f;

        // 衝突後の縦速度の最低比率 (|vy| / speed)。水平スタック防止
        public float minVerticalRatio = 0.2f;

        // 衝突後の横速度の最低比率 (|vx| / speed)。垂直スタック防止
        public float minHorizontalRatio = 0.05f;

        // 発射時のランダム横ずれ範囲（[-N, +N] の一様乱数を初速のXに混ぜる）
        public float launchHorizontalRange = 0.5f;
    }
}
