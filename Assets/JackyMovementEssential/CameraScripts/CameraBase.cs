using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraBase : MonoBehaviour
{
    [SerializeField] private CameraMode cameraMode = CameraMode.BaseTest;
    [SerializeField] private CameraActivationPolicy activationPolicy = CameraActivationPolicy.ModeBound;
    [SerializeField] private MonoBehaviour[] reliedCameraComponents;
    [SerializeField] private bool deactivateSelfComponent = true;
    [SerializeField] protected Transform target;

    public CameraMode CameraMode => cameraMode;
    public CameraActivationPolicy ActivationPolicy => activationPolicy;
    public Transform Target => target;
    public bool HasTarget => target != null;

    public Camera CachedCamera { get; private set; }
    private bool isRegistered;

    protected virtual void Awake()
    {
        CachedCamera = GetComponent<Camera>();
        DeactivateCamera(); // Start with the camera disabled by default. It will be activated by the AllCameraManager when needed.

        if (AllCameraManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(CameraBase)}] No {nameof(AllCameraManager)} instance found. Camera '{name}' will not be registered.", this);
            return;
        }

        AllCameraManager.Instance.RegisterCamera(this);
        isRegistered = true;
    }

    protected virtual void OnDestroy()
    {
        if (isRegistered && AllCameraManager.Instance != null)
            AllCameraManager.Instance.UnRegisterCamera(this);
    }

    public virtual void LateUpdate()
    {
        // BaseCamera doesn't do anything in LateUpdate, but derived classes can override this method to implement their own camera behavior.
    }

    public virtual void Update()
    {
        // BaseCamera doesn't do anything in Update, but derived classes can override this method to implement their own camera behavior.
    }

    public virtual void ActivateCamera()
    {
        CachedCamera.enabled = true;
        SetReliedCameraComponentsEnabled(true);
        if (deactivateSelfComponent)
        {
            this.enabled = true;
        }
    }

    public virtual void DeactivateCamera()
    {
        CachedCamera.enabled = false;
        SetReliedCameraComponentsEnabled(false);
        if (deactivateSelfComponent)
        {
            this.enabled = false;
        }
    }

    private void SetReliedCameraComponentsEnabled(bool enabled)
    {
        if (reliedCameraComponents == null)
            return;

        foreach (var component in reliedCameraComponents)
        {
            if (component == null)
                continue;

            if (component is IReliedCameraComponent)
            {
                component.enabled = enabled;
            }
            else
            {
                Debug.LogError($"CameraBase {name} has a component in reliedCameraComponents that does not implement IReliedCameraComponent, Please Check. Component: {component}", this);
            }
        }
    }

    public virtual void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public virtual void ClearTarget()
    {
        SetTarget(null);
    }
}

public interface IReliedCameraComponent
{
    CameraBase ReliedCamera { get; }
}
