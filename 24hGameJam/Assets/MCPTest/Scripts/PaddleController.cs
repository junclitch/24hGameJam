using UnityEngine;
using UnityEngine.InputSystem;

namespace MCPTest
{
    /// <summary>
    /// パドルの操作担当。キー入力を読み取り、PaddleStats に従って横方向に動かす。
    /// GameState が Playing のときだけ動作する（Pause/GameOver中は止まる）。
    /// 数値はすべて PaddleStats アセットから読み込み、このクラス自体は数値を持たない。
    /// </summary>
    public class PaddleController : MonoBehaviour
    {
        // インスペクタから PaddleStats アセットを差し込む
        [SerializeField] private PaddleStats stats;

        public PaddleStats Stats => stats;

        void Update()
        {
            // FSMによるゲート: プレイ中以外は入力を一切受け付けない
            if (BreakoutGameManager.State != GameState.Playing) return;
            if (stats == null) return;

            // 新Input System: Keyboard.current で現在の入力状態を取得
            float h = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) h -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h += 1f;
            }

            Vector3 p = transform.position;
            p.x += h * stats.speed * Time.deltaTime;
            // 壁の外にはみ出さないようクランプ
            p.x = Mathf.Clamp(p.x, stats.minX, stats.maxX);
            transform.position = p;
        }
    }
}
