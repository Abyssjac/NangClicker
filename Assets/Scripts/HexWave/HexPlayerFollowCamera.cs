using System.Collections;
using UnityEngine;

namespace NangClicker.HexWave
{
    /// <summary>Uses the package follow camera with a fixed-height target; wave displacement never moves the camera vertically.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class HexPlayerFollowCamera : PlayerCameraFollow
    {
        [SerializeField] private Transform followSource;
        [SerializeField] private bool followActivePlayer = true;
        [SerializeField] private float followPlaneHeight = 1.5f;
        [SerializeField] private bool activateOnStart = true;
        private Transform planarTarget;
        private Transform boundSource;
        public Transform FollowSource => followSource;
        public float FollowPlaneHeight => followPlaneHeight;
        public Vector3 PlanarFollowPosition => planarTarget != null ? planarTarget.position : Vector3.zero;

        protected override void Awake()
        {
            RefreshFollowTarget();
            base.Awake();
            // CameraBase may initially disable this component while waiting for its manager.
            if (activateOnStart) StartCoroutine(ActivateWhenRegistered());
        }

        private IEnumerator ActivateWhenRegistered()
        {
            while (AllCameraManager.Instance == null ||
                !AllCameraManager.Instance.FindCamerasByMode(CameraMode).Contains(this)) yield return null;
            if (!AllCameraManager.Instance.HasCameraOverride)
                AllCameraManager.Instance.SwitchCameraMode(CameraMode);
        }

        public override void SetTarget(Transform newTarget)
        {
            followSource = newTarget;
            RefreshFollowTarget(false);
        }

        private void RefreshFollowTarget(bool resolveActivePlayer = true)
        {
            if (resolveActivePlayer && followActivePlayer && PlayerManager.Instance != null && PlayerManager.Instance.ActivePlayer != null)
                followSource = PlayerManager.Instance.ActivePlayer.transform;
            if (followSource == null)
            {
                boundSource = null;
                base.SetTarget(null);
                return;
            }
            if (planarTarget == null)
            {
                var proxy = new GameObject("Horizontal Camera Target") { hideFlags = HideFlags.HideAndDontSave };
                planarTarget = proxy.transform;
                // Keep the transient proxy in the camera's scene without inheriting camera/player motion.
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(proxy, gameObject.scene);
            }
            var sourcePosition = followSource.position;
            planarTarget.position = new Vector3(sourcePosition.x, followPlaneHeight, sourcePosition.z);
            planarTarget.rotation = Quaternion.identity;
            if (boundSource != followSource || target != planarTarget)
            {
                boundSource = followSource;
                base.SetTarget(null);
                base.SetTarget(planarTarget);
            }
        }

        public override void ActivateCamera()
        {
            RefreshFollowTarget();
            base.ActivateCamera();
        }

        public override void LateUpdate()
        {
            RefreshFollowTarget();
            base.LateUpdate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (planarTarget != null) Destroy(planarTarget.gameObject);
        }
    }
}
