using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrollAction
{
    /// <summary>
    /// ScrollAction シーンのゲームサイクル管理。
    /// Player の Y が閾値を下回ったらゲームオーバー、GoalTrigger 接触でゲームクリアへシーン遷移する。
    /// 制限時間 (timeLimit) > 0 ならタイマーを画面中央上部に常時表示し、0 で時間切れゲームオーバー。
    /// 一度遷移を始めたら以降は二重発火しないようガードする。
    /// </summary>
    public class GameCycleManager : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private ActionInventory inventory;
        [SerializeField] private UIFontAsset uiFont;

        // この Y 値を下回ったらゲームオーバー
        [SerializeField] private float gameOverYThreshold;

        // 遷移先シーン名 (Build Settings 登録済みであること)
        [SerializeField] private string gameOverSceneName;
        [SerializeField] private string gameClearSceneName;

        // 「リスタート」ボタンで遷移する Shop シーン名 (詰み回避用)
        [SerializeField] private string shopSceneName;

        [Header("制限時間 (sec)。0 以下なら無制限")]
        [SerializeField] private float timeLimit;

        // 残り時間 (sec)。OnEnable で timeLimit からコピー、Update で減算
        private float remainingTime;

        // 1度でも遷移を発火したら以降の判定を止めるためのガード
        private bool transitioning;

        // OnGUI 用スタイル。lazy init で生成 (GUI.skin が OnGUI 内でしか取れないため)
        private GUIStyle timerStyle;
        private GUIStyle livesStyle;
        private GUIStyle restartButtonStyle;
        private const int TimerFontSize = 36;
        private const int LivesFontSize = 26;
        private const int RestartButtonFontSize = 20;
        // ウィンドウ表示時に中央揃えや右下ボタン位置が崩れるのを防ぐため、1920x1080 設計を基準に GUI.matrix で縮小する。
        // フルスクリーン (>=1920x1080) のときは scale=1 で従来挙動。
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        void OnEnable()
        {
            GoalTrigger.OnGoalReached += HandleGoalReached;
            remainingTime = timeLimit;
        }

        void OnDisable()
        {
            GoalTrigger.OnGoalReached -= HandleGoalReached;
        }

        /// <summary>毎フレーム Player の Y を監視 + 制限時間を減算。</summary>
        void Update()
        {
            if (transitioning || player == null) return;

            if (player.transform.position.y < gameOverYThreshold)
            {
                GoToScene(gameOverSceneName);
                return;
            }

            if (timeLimit > 0f)
            {
                remainingTime -= Time.deltaTime;
                if (remainingTime <= 0f)
                {
                    remainingTime = 0f;
                    GoToScene(gameOverSceneName);
                }
            }
        }

        /// <summary>
        /// 画面中央上部にタイマーを常時描画 (残り 10 秒以下で赤)。
        /// inventory が割当てられていればその直下に残機数も表示する (残機 ≤1 で警告色)。
        /// </summary>
        void OnGUI()
        {
            if (timeLimit <= 0f) return;
            EnsureStyles();

            var prevMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight, 1f);
            if (scale < 1f)
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float logicalW = Screen.width / scale;
            float logicalH = Screen.height / scale;

            // タイマー
            int sec = Mathf.CeilToInt(remainingTime);
            string timerText = $"残り {sec:D2}";
            timerStyle.normal.textColor = remainingTime <= 10f
                ? new Color(1f, 0.3f, 0.3f, 1f)
                : Color.white;
            var ts = timerStyle.CalcSize(new GUIContent(timerText));
            float tx = (logicalW - ts.x) * 0.5f;
            float ty = 16f;
            DrawLabelWithBackdrop(tx, ty, ts.x, TimerFontSize, timerText, timerStyle);

            // 残機 (タイマーの直下、中央揃え)
            if (inventory != null)
            {
                int lives = GetLifeCount();
                string livesText = $"残機 x {lives}";
                livesStyle.normal.textColor = lives <= 1
                    ? new Color(1f, 0.55f, 0.2f, 1f)
                    : Color.white;
                var ls = livesStyle.CalcSize(new GUIContent(livesText));
                float lx = (logicalW - ls.x) * 0.5f;
                float ly = ty + LabelBlockHeight(TimerFontSize) + 12f;
                DrawLabelWithBackdrop(lx, ly, ls.x, LivesFontSize, livesText, livesStyle);
            }

            // リスタートボタン (画面右下) — 押すと Shop シーンへ。詰み回避用
            string label = "リスタート (Shop へ)";
            var bs = restartButtonStyle.CalcSize(new GUIContent(label));
            float buttonH = LabelBlockHeight(RestartButtonFontSize);
            float bw = bs.x + 32f;
            float bh = buttonH;
            float bx = logicalW - bw - 24f;
            float by = logicalH - bh - 24f;
            if (GUI.Button(new Rect(bx, by, bw, bh), label, restartButtonStyle))
            {
                if (!string.IsNullOrEmpty(shopSceneName)) GoToScene(shopSceneName);
            }

            GUI.matrix = prevMatrix;
        }

        /// <summary>所持中の LifeAction 数を返す。未所持なら 0。</summary>
        private int GetLifeCount()
        {
            foreach (var slot in inventory.owned)
            {
                if (slot?.action is LifeAction && slot.count > 0) return slot.count;
            }
            return 0;
        }

        private void EnsureStyles()
        {
            if (timerStyle != null) return;
            timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = TimerFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            timerStyle.normal.textColor = Color.white;

            livesStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = LivesFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            livesStyle.normal.textColor = Color.white;

            restartButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = RestartButtonFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            if (uiFont != null && uiFont.Font != null)
            {
                timerStyle.font = uiFont.Font;
                livesStyle.font = uiFont.Font;
                restartButtonStyle.font = uiFont.Font;
            }
        }

        /// <summary>
        /// fontSize から 「Box+Label ブロックの安全高さ」 を返す。
        /// CJK ダイナミックフォント (NotoSansJP-VF) は glyph 高が fontSize×1.5〜1.8 に達する、
        /// IMGUI の GUIStyle.CalcSize はそれを不正確に返しがちなため、fontSize×2.0 をブロック高として使う。
        /// </summary>
        private static float LabelBlockHeight(int fontSize) => fontSize * 2.0f;

        /// <summary>
        /// 背景黒 > テキスト の順で描画し、GUI.Box の内側パディング/ボーダーに依存せず
        /// テキストを厳密に中央揃えする。w は CalcSize().x 、labelStyle は alignment=MiddleCenter 前提。
        /// </summary>
        private static void DrawLabelWithBackdrop(float x, float y, float w, int fontSize, string text, GUIStyle style)
        {
            float h = LabelBlockHeight(fontSize);
            // 背景 (半透明黒)
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x - 16f, y, w + 32f, h), Texture2D.whiteTexture);
            GUI.color = prev;
            // テキスト (同じ rect に MiddleCenter で描画)
            GUI.Label(new Rect(x - 16f, y, w + 32f, h), text, style);
        }


        private void HandleGoalReached()
        {
            if (transitioning) return;
            GoToScene(gameClearSceneName);
        }

        private void GoToScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            transitioning = true;
            SceneManager.LoadScene(sceneName);
        }
    }
}
