using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrollAction
{
    /// <summary>
    /// ScrollAction シーンのゲームサイクル管理。
    /// Player の Y が閾値を下回ったらゲームオーバー、GoalTrigger 接触でゲームクリアへシーン遷移する。
    /// 一度遷移を始めたら以降は二重発火しないようガードする。
    /// </summary>
    public class GameCycleManager : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        // この Y 値を下回ったらゲームオーバー
        [SerializeField] private float gameOverYThreshold;

        // 遷移先シーン名 (Build Settings 登録済みであること)
        [SerializeField] private string gameOverSceneName;
        [SerializeField] private string gameClearSceneName;

        // 1度でも遷移を発火したら以降の判定を止めるためのガード
        private bool transitioning;

        void OnEnable()
        {
            GoalTrigger.OnGoalReached += HandleGoalReached;
        }

        void OnDisable()
        {
            GoalTrigger.OnGoalReached -= HandleGoalReached;
        }

        /// <summary>毎フレーム Player の Y を監視。閾値未満ならゲームオーバー遷移を予約する。</summary>
        void Update()
        {
            if (transitioning || player == null) return;
            if (player.transform.position.y < gameOverYThreshold)
                GoToScene(gameOverSceneName);
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
