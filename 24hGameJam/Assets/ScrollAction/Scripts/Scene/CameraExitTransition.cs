using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrollAction
{
    /// <summary>
    /// 指定ターゲット (プレイヤー) がカメラ範囲の右端を越えたら別シーンへ遷移する。
    /// Shop シーンのようにカメラが追従しない固定カメラ構成で、画面外に出たら次シーンへ進む用途。
    /// 連続発火を防ぐため一度遷移を開始したらフラグでロックする。
    /// </summary>
    public class CameraExitTransition : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera cam;

        // 遷移先シーン名 (Build Settings に登録されている名前)
        [SerializeField] private string nextSceneName;

        // 1度トリガしたら多重ロード防止のためロックする
        private bool transitioning;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            if (transitioning || target == null || cam == null) return;
            if (string.IsNullOrEmpty(nextSceneName)) return;

            // ビューポート座標 x は左端0/右端1。1を越えたら画面右外に出たと判定
            Vector3 vp = cam.WorldToViewportPoint(target.position);
            if (vp.x > 1f && vp.z > 0f)
            {
                transitioning = true;
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }
}
