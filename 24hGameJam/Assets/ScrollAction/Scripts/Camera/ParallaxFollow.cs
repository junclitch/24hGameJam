using UnityEngine;

namespace ScrollAction
{
    /// <summary>
    /// 指定カメラ（未指定時は MainCamera）のXY位置に追従し、
    /// 画面相対で常に同じ位置に見える背景要素（月や太陽など）を表現する。
    /// scrollFactor=0 で完全固定（純粋な"画面貼り付け"）、1 で前景と同じ動き、
    /// 0〜1 の間でいわゆるパララックス（奥行きに応じた緩やかな追従）になる。
    /// Z は元の値を保持し前後関係を崩さない。
    /// </summary>
    public class ParallaxFollow : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        // カメラからの画面相対オフセット (右上に置きたい場合は両方とも正の値)
        [SerializeField] private Vector2 offset;

        // 0 = 完全固定 / 1 = 前景と等速。中間でパララックス
        [SerializeField, Range(0f, 1f)] private float scrollFactor;

        private Vector3 baseWorld;
        private Vector3 baseCamera;
        private float zKeep;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            zKeep = transform.position.z;
            baseWorld = transform.position;
            if (targetCamera != null) baseCamera = targetCamera.transform.position;
        }

        /// <summary>
        /// LateUpdate でカメラ追従後の位置を反映。
        /// 値はインスペクタの offset を「初期カメラ位置からの相対オフセット」として扱い、
        /// 以降のカメラ移動分に scrollFactor を掛けて加算する。
        /// </summary>
        void LateUpdate()
        {
            if (targetCamera == null) return;

            Vector3 camDelta = targetCamera.transform.position - baseCamera;
            float x = baseWorld.x + offset.x + camDelta.x * scrollFactor;
            float y = baseWorld.y + offset.y + camDelta.y * scrollFactor;
            transform.position = new Vector3(x, y, zKeep);
        }
    }
}
