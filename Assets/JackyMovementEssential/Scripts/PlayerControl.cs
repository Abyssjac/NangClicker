using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour
{
    [Header("Action References (drag from .inputactions)")]
    [SerializeField] private InputActionReference move;
    [SerializeField] private InputActionReference interact;
    [SerializeField] private InputActionReference dash;
    [SerializeField] private InputActionReference jump;

    [Header("Optional: Other actions to manage")]
    [Tooltip("���������/У��/���ص� actions������ Look, Jump, Inventory, etc.��")]
    [SerializeField] private List<InputActionReference> extraActions = new();

    [Header("Init Debug / Validation")]
    [SerializeField] private bool logActionsOnInit = true;
    [SerializeField] private bool autoEnableOnStart = true;

    [Header("Startup Toggles")]
    [Tooltip("����ʱ������Щ action��ǿ�������ã�����ƴд����")]
    [SerializeField] private List<InputActionReference> disableOnStart = new();

    [Tooltip("����ʱ������Щ action��ǿ�������ã�")]
    [SerializeField] private List<InputActionReference> enableOnStart = new();

    // === Public API ===
    public Vector2 MoveInput() => ReadValue<Vector2>(move);

    public bool InteractTriggered() => WasTriggered(interact);
    public bool DashTriggered() => WasTriggered(dash);
    public bool JumpTriggered() => WasTriggered(jump);

    /// <summary>Enable/Disable ���� action���ɸ��ⲿϵͳ�ã�</summary>
    public void SetEnabled(InputActionReference actionRef, bool enabled)
    {
        var a = GetAction(actionRef);
        if (a == null) return;

        if (enabled) a.Enable();
        else a.Disable();
    }

    /// <summary>һ���Կ��ض�� action</summary>
    public void SetEnabled(IEnumerable<InputActionReference> refs, bool enabled)
    {
        if (refs == null) return;
        foreach (var r in refs) SetEnabled(r, enabled);
    }

    private void Awake()
    {
        // ��ѡ�������ϣ����������ȫ������ PlayerInput��Ҳ���Բ� RequireComponent(PlayerInput)
        // ֻҪ���õ� InputActionReference ����ͬһ�� asset�������ܹ�����
    }

    private void OnEnable()
    {
        // InputActionReference Ĭ�ϲ����Զ� Enable��ȡ������� PlayerInput/���Ƿ��ֶ� Enable��
        if (autoEnableOnStart)
        {
            EnableCoreIfValid();
            SetEnabled(enableOnStart, true);
            SetEnabled(disableOnStart, false);
        }

        if (logActionsOnInit)
            LogAllConfiguredActions();
    }

    private void OnDisable()
    {
        // ����ͨ����ǿ�� Disable������Ӱ����ϵͳ/���
        // ������볹�ס�������߾ͽ������롱�����Ըĳ� DisableCoreIfValid();
    }

    // =========================
    // Internal helpers
    // =========================

    private void EnableCoreIfValid()
    {
        // ���� actions ������˾� enable
        TryEnable(move);
        TryEnable(interact);
        TryEnable(dash);
        TryEnable(jump);

        // ���� actions Ҳ�����ã���������
        if (extraActions != null)
        {
            foreach (var a in extraActions) TryEnable(a);
        }
    }

    private void DisableCoreIfValid()
    {
        TryDisable(move);
        TryDisable(interact);
        TryDisable(dash);
        TryDisable(jump);

        if (extraActions != null)
        {
            foreach (var a in extraActions) TryDisable(a);
        }
    }

    private static InputAction GetAction(InputActionReference actionRef)
    {
        if (actionRef == null)
        {
            Debug.LogWarning("[PlayerControl] InputActionReference is NULL (not assigned).");
            return null;
        }

        if (actionRef.action == null)
        {
            Debug.LogWarning($"[PlayerControl] InputActionReference '{actionRef.name}' has NULL action. " +
                             $"(Did you drag the correct action from the asset?)");
            return null;
        }

        return actionRef.action;
    }

    private static void TryEnable(InputActionReference actionRef)
    {
        var a = GetAction(actionRef);
        if (a == null) return;

        if (!a.enabled) a.Enable();
    }

    private static void TryDisable(InputActionReference actionRef)
    {
        var a = GetAction(actionRef);
        if (a == null) return;

        if (a.enabled) a.Disable();
    }

    private static T ReadValue<T>(InputActionReference actionRef) where T : struct
    {
        var a = GetAction(actionRef);
        if (a == null) return default;
        return a.ReadValue<T>();
    }

    private static bool WasTriggered(InputActionReference actionRef)
    {
        var a = GetAction(actionRef);
        if (a == null) return false;
        return a.triggered;
    }

    private void LogAllConfiguredActions()
    {
        Debug.Log("[PlayerControl] ===== Configured Actions =====", this);

        LogOne("Move", move);
        LogOne("Interact", interact);
        LogOne("Dash", dash);
        LogOne("Jump", jump);

        if (extraActions != null && extraActions.Count > 0)
        {
            Debug.Log("[PlayerControl] ----- Extra Actions -----", this);
            foreach (var a in extraActions)
            {
                LogOne(a != null ? a.name : "NULL", a);
            }
        }

        if (enableOnStart != null && enableOnStart.Count > 0)
        {
            Debug.Log("[PlayerControl] ----- Enable On Start -----", this);
            foreach (var a in enableOnStart) LogOne(a != null ? a.name : "NULL", a);
        }

        if (disableOnStart != null && disableOnStart.Count > 0)
        {
            Debug.Log("[PlayerControl] ----- Disable On Start -----", this);
            foreach (var a in disableOnStart) LogOne(a != null ? a.name : "NULL", a);
        }

        Debug.Log("[PlayerControl] =============================", this);
    }

    private void LogOne(string label, InputActionReference actionRef)
    {
        if (actionRef == null)
        {
            Debug.LogWarning($"[PlayerControl] {label}: NOT ASSIGNED", this);
            return;
        }

        var a = actionRef.action;
        if (a == null)
        {
            Debug.LogWarning($"[PlayerControl] {label}: assigned ref '{actionRef.name}' but action is NULL", this);
            return;
        }

        string mapName = a.actionMap != null ? a.actionMap.name : "(no map)";
        Debug.Log($"[PlayerControl] {label}: action='{a.name}', map='{mapName}', enabled={a.enabled}, type={a.type}", this);
    }
}
