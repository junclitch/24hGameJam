using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// ブロック1種類分の定義 (ScriptableObject)。
    /// 色／スコアなどの「種類」をアセットとして定義し、Brick コンポーネントから参照する。
    /// 値はすべて .asset 側で設定する。
    /// アクションゲームの EnemyType / ItemType と同じパターンの雛形。
    /// </summary>
    [CreateAssetMenu(fileName = "BrickType", menuName = "MCPTest/Brick Type")]
    public class BrickType : ScriptableObject
    {
        // 破壊時に加算されるスコア
        public int score;

        // SpriteRenderer に流し込む色
        public Color color;
    }
}
