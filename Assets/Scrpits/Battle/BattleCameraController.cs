using BattleSystem;
using UnityEngine;

/// <summary>
/// 战斗场景专用镜头控制器。
///
/// 设计目标：
/// 1. 开场保持稳定总览；
/// 2. 可自动聚焦当前行动单位 / 当前目标；
/// 3. 保留固定战斗舞台的“导演镜头”感，而不是玩家自由控制；
/// 4. 支持轻量镜头震动，适合受击 / Break / 大招演出。
///
/// 使用方式：
/// - 将本脚本挂到 Main Camera；
/// - 可选拖入 BattleManager；
/// - 调整相机到你想要的默认总览角度后，在 Inspector 右上角菜单执行：
///   "Capture Current As Default Pose"；
/// - 运行时可由 BattleManager 或其他演出脚本调用 FocusOnUnit / ResetToOverview / PlayShake。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class BattleCameraController : MonoBehaviour
{
    public enum CameraMode
    {
        Overview,
        FocusTarget,
        FocusPoint,
        Manual
    }

    [Header("引用")]
    public BattleManager battleManager;
    public Camera targetCamera;
    public Transform overviewAnchor;

    [Header("启动 / 总览")]
    public bool snapToOverviewOnStart = true;
    public bool autoBuildOverviewFromSceneUnits = true;
    public Vector3 overviewWorldOffset = new Vector3(0f, 1.25f, 0f);
    [Min(0f)] public float overviewPadding = 1.75f;
    public float overviewOrthoSize = 5.5f;
    public float overviewFieldOfView = 30f;

    [Header("自动聚焦")]
    public bool autoFocusCurrentUnit = true;
    public bool focusSelectedTargetDuringTargetSelect = true;
    public bool returnToOverviewWhenIdle = true;
    public bool autoZoomWhenFocusing = false;
    public bool preventZoomingInPastOverview = true;
    [Range(0f, 1f)] public float focusFollowWeight = 0.45f;
    public Vector3 playerFocusOffset = new Vector3(0.8f, 0.4f, 0f);
    public Vector3 enemyFocusOffset = new Vector3(-0.8f, 0.4f, 0f);
    public float focusHeight = 1f;
    public float focusOrthoSize = 4.75f;
    public float focusFieldOfView = 27f;
    public bool rotateTowardFocusPoint = false;

    [Header("平滑")]
    [Min(0.01f)] public float positionSmoothTime = 0.18f;
    [Min(0.01f)] public float rotationLerpSpeed = 8f;
    [Min(0.01f)] public float zoomSmoothTime = 0.18f;
    public bool usePositionClamp = false;
    public Vector2 clampX = new Vector2(-12f, 12f);
    public Vector2 clampY = new Vector2(-2f, 8f);

    [Header("镜头震动")]
    public float defaultShakeStrength = 0.18f;
    public float defaultShakeDuration = 0.18f;
    [Range(5f, 60f)] public float shakeFrequency = 24f;

    public CameraMode Mode => _mode;

    private CameraMode _mode = CameraMode.Overview;
    private Vector3 _defaultPosition;
    private Quaternion _defaultRotation;
    private float _defaultOrthoSize;
    private float _defaultFieldOfView;

    private Vector3 _overviewLookPoint;
    private float _resolvedOverviewOrthoSize;
    private float _resolvedOverviewFieldOfView;

    private Transform _focusTarget;
    private Vector3 _focusPoint;
    private Vector3 _focusTargetOffset;
    private BattleUnit _lastAutoFocusedUnit;

    private Vector3 _positionVelocity;
    private float _zoomVelocity;

    private float _shakeTimeRemaining;
    private float _shakeDurationCache;
    private float _shakeStrength;

    void Reset()
    {
        targetCamera = GetComponent<Camera>();
        CaptureDefaultPoseInternal(false);
    }

    void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();

        CaptureDefaultPoseInternal(false);

        if (autoBuildOverviewFromSceneUnits)
            RebuildOverviewFromSceneUnits();
        else
            RefreshOverviewAnchorOnly();

        InitializeDesiredStateFromCurrentCamera();
    }

    void Start()
    {
        if (snapToOverviewOnStart)
            ResetToOverview(true);
    }

    void LateUpdate()
    {
        if (autoFocusCurrentUnit)
            UpdateAutoFocusFromBattleManager();

        UpdateCameraPose();
        ApplyCameraPose();
    }

    [ContextMenu("Capture Current As Default Pose")]
    public void CaptureCurrentAsDefaultPose()
    {
        CaptureDefaultPoseInternal(true);
        Debug.Log("[BattleCamera] 已记录当前相机姿态为默认总览姿态。", this);
    }

    [ContextMenu("Reset To Overview")]
    public void ResetToOverviewImmediate()
    {
        ResetToOverview(true);
    }

    [ContextMenu("Rebuild Overview From Scene Units")]
    public void RebuildOverviewFromSceneUnitsContext()
    {
        RebuildOverviewFromSceneUnits();
        ResetToOverview(true);
    }

    public void CaptureDefaultPose()
    {
        CaptureDefaultPoseInternal(true);
    }

    public void ResetToOverview(bool immediate = false)
    {
        _mode = CameraMode.Overview;
        _focusTarget = null;
        _focusPoint = _overviewLookPoint;

        if (immediate)
            SnapToPose(GetDesiredPositionForCurrentMode(), GetDesiredRotationForCurrentMode(), GetDesiredZoomForCurrentMode());
    }

    public void SetManualMode()
    {
        _mode = CameraMode.Manual;
        _focusTarget = null;
    }

    public void FocusOnUnit(BattleUnit unit, bool immediate = false)
    {
        if (unit == null)
        {
            ResetToOverview(immediate);
            return;
        }

        _mode = CameraMode.FocusTarget;
        _focusTarget = unit.transform;
        _focusTargetOffset = GetFocusOffsetForUnit(unit);
        _focusPoint = _focusTarget.position + _focusTargetOffset;

        if (immediate)
            SnapToPose(GetDesiredPositionForCurrentMode(), GetDesiredRotationForCurrentMode(), GetDesiredZoomForCurrentMode());
    }

    public void FocusOnTransform(Transform target, Vector3 worldOffset, bool immediate = false)
    {
        if (target == null)
        {
            ResetToOverview(immediate);
            return;
        }

        _mode = CameraMode.FocusTarget;
        _focusTarget = target;
        _focusTargetOffset = worldOffset;
        _focusPoint = _focusTarget.position + _focusTargetOffset;

        if (immediate)
            SnapToPose(GetDesiredPositionForCurrentMode(), GetDesiredRotationForCurrentMode(), GetDesiredZoomForCurrentMode());
    }

    public void FocusOnPoint(Vector3 worldPoint, bool immediate = false)
    {
        _mode = CameraMode.FocusPoint;
        _focusTarget = null;
        _focusPoint = worldPoint;

        if (immediate)
            SnapToPose(GetDesiredPositionForCurrentMode(), GetDesiredRotationForCurrentMode(), GetDesiredZoomForCurrentMode());
    }

    public void PlayShake()
    {
        PlayShake(defaultShakeStrength, defaultShakeDuration);
    }

    public void PlayShake(float strength, float duration)
    {
        if (strength <= 0f || duration <= 0f)
            return;

        _shakeStrength = Mathf.Max(_shakeStrength, strength);
        _shakeTimeRemaining = Mathf.Max(_shakeTimeRemaining, duration);
        _shakeDurationCache = Mathf.Max(_shakeDurationCache, duration);
    }

    public void RebuildOverviewFromSceneUnits()
    {
        RefreshOverviewAnchorOnly();

        BattleUnit[] units = FindObjectsOfType<BattleUnit>();
        if (units == null || units.Length == 0)
            return;

        bool hasBounds = false;
        Bounds bounds = default;

        foreach (BattleUnit unit in units)
        {
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = new Bounds(unit.transform.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(unit.transform.position);
            }
        }

        if (!hasBounds)
            return;

        if (overviewAnchor == null)
            _overviewLookPoint = bounds.center + overviewWorldOffset;

        float halfWidth = bounds.size.x * 0.5f + overviewPadding;
        float halfHeight = bounds.size.y * 0.5f + overviewPadding;

        if (targetCamera.orthographic)
        {
            float aspect = Mathf.Max(0.1f, targetCamera.aspect);
            float heightFromWidth = halfWidth / aspect;
            _resolvedOverviewOrthoSize = Mathf.Max(_defaultOrthoSize, overviewOrthoSize, halfHeight, heightFromWidth);
        }
        else
        {
            float spread = Mathf.Max(bounds.size.x, bounds.size.y);
            float t = Mathf.InverseLerp(2f, 12f, spread + overviewPadding);
            float computedFov = Mathf.Lerp(focusFieldOfView, overviewFieldOfView, Mathf.Clamp01(t));
            _resolvedOverviewFieldOfView = Mathf.Max(_defaultFieldOfView, computedFov);
        }
    }

    void RefreshOverviewAnchorOnly()
    {
        _overviewLookPoint = overviewAnchor != null
            ? overviewAnchor.position + overviewWorldOffset
            : transform.position + transform.forward * 10f + overviewWorldOffset;

        _resolvedOverviewOrthoSize = Mathf.Max(overviewOrthoSize, _defaultOrthoSize);
        _resolvedOverviewFieldOfView = Mathf.Max(overviewFieldOfView, _defaultFieldOfView);
    }

    void CaptureDefaultPoseInternal(bool refreshDesiredState)
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation;
        _defaultOrthoSize = targetCamera.orthographic ? targetCamera.orthographicSize : overviewOrthoSize;
        _defaultFieldOfView = targetCamera.orthographic ? overviewFieldOfView : targetCamera.fieldOfView;

        if (_resolvedOverviewOrthoSize <= 0f)
            _resolvedOverviewOrthoSize = overviewOrthoSize;
        if (_resolvedOverviewFieldOfView <= 0f)
            _resolvedOverviewFieldOfView = overviewFieldOfView;

        if (refreshDesiredState)
            InitializeDesiredStateFromCurrentCamera();
    }

    void InitializeDesiredStateFromCurrentCamera()
    {
        _positionVelocity = Vector3.zero;
        _zoomVelocity = 0f;
    }

    void UpdateAutoFocusFromBattleManager()
    {
        if (battleManager == null)
            return;

        BattleUnit autoTarget = null;

        switch (battleManager.state)
        {
            case BattleState.PlayerTurn:
            case BattleState.CommandSelect:
            case BattleState.Busy:
            case BattleState.EnemyTurn:
                autoTarget = ResolveAutoFocusTarget();
                break;

            case BattleState.TargetSelect:
                autoTarget = ResolveAutoFocusTarget();
                break;

            default:
                autoTarget = null;
                break;
        }

        if (autoTarget != null)
        {
            if (_lastAutoFocusedUnit != autoTarget || (_mode != CameraMode.FocusTarget && _mode != CameraMode.FocusPoint))
                FocusOnUnit(autoTarget, false);
        }
        else if (returnToOverviewWhenIdle && _mode != CameraMode.Overview)
        {
            ResetToOverview(false);
        }

        _lastAutoFocusedUnit = autoTarget;
    }

    BattleUnit ResolveAutoFocusTarget()
    {
        if (battleManager.state == BattleState.TargetSelect && focusSelectedTargetDuringTargetSelect)
        {
            BattleUnit selectedTarget = battleManager.CurrentSelectedTarget;
            if (selectedTarget != null)
                return selectedTarget;
        }

        return battleManager.CurrentUnit;
    }

    void UpdateCameraPose()
    {
        if (_mode == CameraMode.Manual)
            return;

        if (_mode == CameraMode.FocusTarget && _focusTarget != null)
            _focusPoint = _focusTarget.position + _focusTargetOffset;
        else if (_mode == CameraMode.Overview)
            _focusPoint = _overviewLookPoint;
    }

    void ApplyCameraPose()
    {
        if (targetCamera == null)
            return;

        Vector3 desiredPosition = GetDesiredPositionForCurrentMode();
        Quaternion desiredRotation = GetDesiredRotationForCurrentMode();
        float desiredZoom = GetDesiredZoomForCurrentMode();

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime));

        if (targetCamera.orthographic)
            targetCamera.orthographicSize = Mathf.SmoothDamp(targetCamera.orthographicSize, desiredZoom, ref _zoomVelocity, zoomSmoothTime);
        else
            targetCamera.fieldOfView = Mathf.SmoothDamp(targetCamera.fieldOfView, desiredZoom, ref _zoomVelocity, zoomSmoothTime);

        ApplyShakeOffset();
    }

    Vector3 GetDesiredPositionForCurrentMode()
    {
        if (_mode == CameraMode.Overview)
            return ClampPosition(_defaultPosition);

        Vector3 delta = (_focusPoint - _overviewLookPoint) * focusFollowWeight;
        Vector3 desired = _defaultPosition + delta;
        return ClampPosition(desired);
    }

    Quaternion GetDesiredRotationForCurrentMode()
    {
        if (!rotateTowardFocusPoint || _mode == CameraMode.Overview)
            return _defaultRotation;

        Vector3 desiredPosition = GetDesiredPositionForCurrentMode();
        Vector3 direction = (_focusPoint - desiredPosition);
        if (direction.sqrMagnitude < 0.0001f)
            return _defaultRotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    float GetDesiredZoomForCurrentMode()
    {
        float overviewZoom = targetCamera.orthographic
            ? Mathf.Max(_defaultOrthoSize, _resolvedOverviewOrthoSize)
            : _resolvedOverviewFieldOfView > 0f ? Mathf.Max(_resolvedOverviewFieldOfView, _defaultFieldOfView) : _defaultFieldOfView;

        if (_mode == CameraMode.Overview)
            return overviewZoom;

        if (!autoZoomWhenFocusing)
            return overviewZoom;

        float focusZoom = targetCamera.orthographic ? focusOrthoSize : focusFieldOfView;

        if (preventZoomingInPastOverview)
            focusZoom = Mathf.Max(focusZoom, overviewZoom);

        return focusZoom;
    }

    Vector3 ClampPosition(Vector3 position)
    {
        if (!usePositionClamp)
            return position;

        position.x = Mathf.Clamp(position.x, clampX.x, clampX.y);
        position.y = Mathf.Clamp(position.y, clampY.x, clampY.y);
        return position;
    }

    Vector3 GetFocusOffsetForUnit(BattleUnit unit)
    {
        Vector3 baseOffset = unit != null && unit.unitType == UnitType.Enemy
            ? enemyFocusOffset
            : playerFocusOffset;

        baseOffset.y += focusHeight;
        return baseOffset;
    }

    void SnapToPose(Vector3 position, Quaternion rotation, float zoom)
    {
        if (targetCamera == null)
            return;

        transform.position = position;
        transform.rotation = rotation;

        if (targetCamera.orthographic)
            targetCamera.orthographicSize = zoom;
        else
            targetCamera.fieldOfView = zoom;
    }

    void ApplyShakeOffset()
    {
        if (_shakeTimeRemaining <= 0f)
            return;

        _shakeTimeRemaining -= Time.deltaTime;

        float fade = Mathf.Clamp01(_shakeTimeRemaining / Mathf.Max(0.0001f, _shakeDurationCache));
        float time = Time.unscaledTime * shakeFrequency;

        Vector3 offset = new Vector3(
            (Mathf.PerlinNoise(time, 0.123f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(0.456f, time) - 0.5f) * 2f,
            0f) * (_shakeStrength * fade);

        transform.position += offset;

        if (_shakeTimeRemaining <= 0f)
        {
            _shakeStrength = 0f;
            _shakeDurationCache = 0f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 anchor = overviewAnchor != null
            ? overviewAnchor.position + overviewWorldOffset
            : _overviewLookPoint;

        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(anchor, 0.25f);
        Gizmos.DrawLine(anchor, anchor + Vector3.up * 0.8f);
    }
}
