using System;
using UnityEngine;

namespace MCPTest
{
    /// <summary>
    /// ゲーム進行（FSM）の管理者。
    /// Brick / BallController が発火する static イベントを購読し、
    /// スコア・残機・状態遷移を集約する。
    /// 各コントローラはこのクラスを直接参照しないが、状態問い合わせは static プロパティ State 経由で行う。
    /// </summary>
    public class BreakoutGameManager : MonoBehaviour
    {
        public int score = 0;
        public int lives = 3;

        // 状態が変化したことを他クラスに通知したい場合に使う static イベント
        public static event Action<GameState> OnStateChanged;

        // 現在のゲーム状態。各コントローラはこの値を見て自分を動かすか決める
        public static GameState State { get; private set; } = GameState.Playing;

        void Awake()
        {
            // 起動時に必ず Playing から始める（テストや再起動時のリセット）
            ChangeState(GameState.Playing);
        }

        // OnEnable/OnDisable で購読・購読解除を対にする。
        // 解除を忘れると、シーン遷移後に死んだインスタンスへイベントが届いて NullRef や多重発火の原因になる。
        void OnEnable()
        {
            Brick.OnAnyDestroyed += HandleBrickDestroyed;
            BallController.OnFell += HandleBallFell;
        }

        void OnDisable()
        {
            Brick.OnAnyDestroyed -= HandleBrickDestroyed;
            BallController.OnFell -= HandleBallFell;
        }

        /// <summary>状態を変更し、購読者に通知する。同じ状態への遷移は無視。</summary>
        void ChangeState(GameState next)
        {
            if (State == next) return;
            State = next;
            OnStateChanged?.Invoke(next);
            Debug.Log($"[Breakout] State -> {next}");
        }

        // ブロック破壊イベントの受け手
        void HandleBrickDestroyed(int points)
        {
            score += points;
            // 残ブロック0でクリア判定
            if (Brick.AliveCount <= 0)
            {
                ChangeState(GameState.Cleared);
            }
        }

        // ボール落下イベントの受け手
        void HandleBallFell()
        {
            lives--;
            // lives==0 でも次の1球は遊べる仕様。負になった瞬間にゲームオーバー
            if (lives < 0)
            {
                ChangeState(GameState.GameOver);
            }
        }

        // IMGUI による画面オーバーレイ表示。
        // 本格的なUIは uGUI / UI Toolkit に置き換える前提で、デバッグ用途のシンプル実装。
        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 32;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(20, 16, 400, 48), $"Score: {score}", style);
            GUI.Label(new Rect(20, 60, 400, 48), $"Lives: {lives}", style);

            if (State == GameState.GameOver)
            {
                DrawCenter("GAME OVER", new Color(1f, 0.3f, 0.3f));
            }
            else if (State == GameState.Cleared)
            {
                DrawCenter("CLEAR!", new Color(0.4f, 1f, 0.5f));
            }
        }

        void DrawCenter(string text, Color color)
        {
            var big = new GUIStyle(GUI.skin.label);
            big.fontSize = 96;
            big.fontStyle = FontStyle.Bold;
            big.alignment = TextAnchor.MiddleCenter;
            big.normal.textColor = color;
            GUI.Label(new Rect(0, Screen.height * 0.5f - 80f, Screen.width, 160f), text, big);
        }
    }
}
