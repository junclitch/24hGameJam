using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 背景オブジェクトをカメラに対して "ほぼ追従" させるパララックス。
    /// scrollFactor=1.0 で完全にカメラに貼り付き、0.0 でワールド固定になる。
    /// 中間値 (例: 0.95) にすると遠景的なわずかな視差が生じ、プレイヤーが動いた時に
    /// 月などの遠景がかすかに動いて奥行き感が出る。
    /// </summary>
    public class ParallaxFollow : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;

        // 0=完全固定 / 1=カメラと完全同期。月のような遠景は 0.9〜0.98 が自然
        [SerializeField, Range(0f, 1f)] private float scrollFactor;

        // 起動時のカメラ位置を基準とした相対位置のうち、X/Y のオフセット成分
        // (画面右上に固定したい場合などはここで指定する想定だが、現状はワールド絶対位置の補正に使う)
        [SerializeField] private Vector2 offset;

        // Awake 時点でのワールド位置を基準点として保持。以後はこの基準＋カメラ移動分でずらす
        private Vector3 baseWorldPos;
        private Vector3 baseCameraPos;

        void Awake()
        {
            if (cameraTransform == null && UnityEngine.Camera.main != null)
                cameraTransform = UnityEngine.Camera.main.transform;

            baseWorldPos = transform.position;
            if (cameraTransform != null) baseCameraPos = cameraTransform.position;
        }

        /// <summary>カメラ移動量に scrollFactor を乗じてワールド位置を補正する。</summary>
        void LateUpdate()
        {
            if (cameraTransform == null) return;

            Vector3 camDelta = cameraTransform.position - baseCameraPos;
            Vector3 next = baseWorldPos
                + new Vector3(offset.x, offset.y, 0f)
                + new Vector3(camDelta.x, camDelta.y, 0f) * scrollFactor;
            // Z は元の値を維持 (2D の手前/奥順序を壊さない)
            next.z = transform.position.z;
            transform.position = next;
        }
    }
}
