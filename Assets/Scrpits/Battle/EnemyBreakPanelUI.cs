using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnemyBreakPanelUI : MonoBehaviour
{
    [Serializable]
    public struct AttackTypeSpriteEntry
    {
        public AttackType attackType;
        public Sprite iconSprite;
    }

    public enum PanelVisibilityMode
    {
        BoundUnitAlwaysVisible,
        OnlyWhenSelectingEnemy,
        KeepLastEnemyVisible
    }

    [Header("战斗绑定")]
    public BattleManager battleManager;
    public BattleUnit fixedTargetUnit;
    public PanelVisibilityMode visibilityMode = PanelVisibilityMode.BoundUnitAlwaysVisible;

    [Header("跟随目标")]
    public bool followWorldTarget = true;
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    public Camera worldCamera;
    public Canvas rootCanvas;

    [Header("根节点引用")]
    public RectTransform panelRect;
    public CanvasGroup canvasGroup;
    public TMP_Text weaknessLabel;
    public string weaknessLabelText = "弱点";

    [Header("护盾显示")]
    public Image shieldIcon;
    public TMP_Text shieldCountText;
    public string shieldTextPrefix = string.Empty;
    public Sprite normalShieldSprite;
    public Sprite breakShieldSprite;
    public Color normalShieldColor = Color.white;
    public Color breakShieldColor = new Color(1f, 0.42f, 0.42f, 1f);

    [Header("弱点槽")]
    public RectTransform slotContainer;
    public WeaknessSlotUI weaknessSlotPrefab;
    public bool autoBuildFromPrefab = true;
    public List<AttackTypeSpriteEntry> attackTypeIconMappings = new List<AttackTypeSpriteEntry>();

    private readonly List<WeaknessSlotUI> slotInstances = new List<WeaknessSlotUI>();
    private BattleUnit boundUnit;
    private BattleUnit lastVisibleEnemy;

    void Awake()
    {
        AutoAssignReferences();
        EnsureCanvasGroup();
        CacheExistingSlots();
        SetVisible(false);
    }

    void OnEnable()
    {
        AutoAssignReferences();
        EnsureCanvasGroup();
        RefreshBinding(true);
    }

    void OnDisable()
    {
        UnbindCurrentUnit();
    }

    void LateUpdate()
    {
        RefreshBinding();
        UpdatePanelPosition();
    }

    public void ForceRefresh()
    {
        RefreshBinding(true);
    }

    public void BindToUnit(BattleUnit unit, BattleManager manager = null)
    {
        fixedTargetUnit = unit;
        if (manager != null)
            battleManager = manager;

        lastVisibleEnemy = unit;
        RefreshBinding(true);
    }

    void RefreshBinding(bool force = false)
    {
        BattleUnit target = ResolveDisplayTarget();
        if (!force && target == boundUnit)
        {
            SetVisible(target != null);
            return;
        }

        BindTarget(target);
    }

    BattleUnit ResolveDisplayTarget()
    {
        if (fixedTargetUnit != null)
        {
            if (fixedTargetUnit.currentHP > 0)
                return fixedTargetUnit;

            return null;
        }

        if (battleManager == null)
            battleManager = FindObjectOfType<BattleManager>();

        if (battleManager == null)
            return null;

        BattleUnit selectedTarget = battleManager.CurrentSelectedTarget;
        if (selectedTarget != null && selectedTarget.unitType == UnitType.Enemy && selectedTarget.currentHP > 0)
        {
            lastVisibleEnemy = selectedTarget;
            return selectedTarget;
        }

        if (visibilityMode == PanelVisibilityMode.OnlyWhenSelectingEnemy)
            return null;

        if (lastVisibleEnemy != null && lastVisibleEnemy.currentHP > 0)
            return lastVisibleEnemy;

        if (battleManager.enemies != null)
        {
            BattleUnit firstAliveEnemy = battleManager.enemies.Find(unit => unit != null && unit.currentHP > 0);
            if (firstAliveEnemy != null)
            {
                lastVisibleEnemy = firstAliveEnemy;
                return firstAliveEnemy;
            }
        }

        return null;
    }

    void BindTarget(BattleUnit unit)
    {
        if (boundUnit == unit)
        {
            SetVisible(unit != null);
            return;
        }

        UnbindCurrentUnit();
        boundUnit = unit;

        if (boundUnit == null)
        {
            ClearPanel();
            SetVisible(false);
            return;
        }

        boundUnit.OnShieldChanged += HandleShieldChanged;
        boundUnit.OnWeaknessStateChanged += HandleWeaknessStateChanged;

        RefreshAll();
        SetVisible(true);
    }

    void UnbindCurrentUnit()
    {
        if (boundUnit == null)
            return;

        boundUnit.OnShieldChanged -= HandleShieldChanged;
        boundUnit.OnWeaknessStateChanged -= HandleWeaknessStateChanged;
        boundUnit = null;
    }

    void HandleShieldChanged(int current, int max)
    {
        RefreshShield();
    }

    void HandleWeaknessStateChanged()
    {
        RefreshWeaknessSlots();
        RefreshShield();
    }

    void RefreshAll()
    {
        if (weaknessLabel != null)
            weaknessLabel.text = weaknessLabelText;

        RefreshShield();
        RefreshWeaknessSlots();
    }

    void RefreshShield()
    {
        if (boundUnit == null)
            return;

        if (shieldCountText != null)
            shieldCountText.text = string.IsNullOrEmpty(shieldTextPrefix)
                ? boundUnit.currentShield.ToString()
                : $"{shieldTextPrefix}{boundUnit.currentShield}";

        if (shieldIcon != null)
        {
            bool isBreak = boundUnit.isBreak;
            Sprite targetSprite = isBreak && breakShieldSprite != null ? breakShieldSprite : normalShieldSprite;
            if (targetSprite != null)
                shieldIcon.sprite = targetSprite;

            shieldIcon.color = isBreak ? breakShieldColor : normalShieldColor;
            shieldIcon.preserveAspect = true;
        }
    }

    void RefreshWeaknessSlots()
    {
        if (slotContainer == null)
            return;

        if (slotInstances.Count == 0)
            CacheExistingSlots();

        int weaknessCount = boundUnit != null && boundUnit.weaknessTypes != null
            ? boundUnit.weaknessTypes.Count
            : 0;

        EnsureSlotCount(weaknessCount);

        for (int i = 0; i < slotInstances.Count; i++)
        {
            WeaknessSlotUI slot = slotInstances[i];
            if (slot == null)
                continue;

            bool shouldShow = boundUnit != null && i < weaknessCount;
            slot.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                slot.ClearState();
                continue;
            }

            AttackType weaknessType = boundUnit.weaknessTypes[i];
            Sprite iconSprite = GetIconSprite(weaknessType);
            bool revealed = boundUnit.IsWeaknessRevealed(weaknessType);
            slot.SetState(weaknessType, iconSprite, revealed);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer);
    }

    void EnsureSlotCount(int targetCount)
    {
        if (slotContainer == null)
            return;

        if (slotInstances.Count == 0)
            CacheExistingSlots();

        while (autoBuildFromPrefab && weaknessSlotPrefab != null && slotInstances.Count < targetCount)
        {
            WeaknessSlotUI newSlot = Instantiate(weaknessSlotPrefab, slotContainer);
            newSlot.gameObject.name = $"WeaknessSlot_{slotInstances.Count}";
            slotInstances.Add(newSlot);
        }
    }

    void CacheExistingSlots()
    {
        slotInstances.Clear();

        if (slotContainer == null)
            return;

        for (int i = 0; i < slotContainer.childCount; i++)
        {
            WeaknessSlotUI slot = slotContainer.GetChild(i).GetComponent<WeaknessSlotUI>();
            if (slot != null)
                slotInstances.Add(slot);
        }
    }

    Sprite GetIconSprite(AttackType attackType)
    {
        for (int i = 0; i < attackTypeIconMappings.Count; i++)
        {
            if (attackTypeIconMappings[i].attackType == attackType)
                return attackTypeIconMappings[i].iconSprite;
        }

        return null;
    }

    void UpdatePanelPosition()
    {
        if (!followWorldTarget || boundUnit == null || panelRect == null || rootCanvas == null)
            return;

        Camera sourceCamera = worldCamera != null ? worldCamera : Camera.main;
        if (sourceCamera == null)
            return;

        Vector3 screenPos = sourceCamera.WorldToScreenPoint(boundUnit.transform.position + worldOffset);
        if (screenPos.z < 0f)
        {
            SetVisible(false);
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        Camera canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, canvasCamera, out Vector2 localPos))
            panelRect.anchoredPosition = localPos;
    }

    void ClearPanel()
    {
        if (shieldCountText != null)
            shieldCountText.text = string.Empty;

        for (int i = 0; i < slotInstances.Count; i++)
        {
            if (slotInstances[i] != null)
            {
                slotInstances[i].ClearState();
                slotInstances[i].gameObject.SetActive(false);
            }
        }
    }

    void SetVisible(bool visible)
    {
        EnsureCanvasGroup();

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void AutoAssignReferences()
    {
        if (panelRect == null)
            panelRect = transform as RectTransform;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (weaknessLabel == null)
            weaknessLabel = transform.Find("Content/WeaknessLabel")?.GetComponent<TMP_Text>();

        if (shieldIcon == null)
            shieldIcon = transform.Find("Content/ShieldGroup/ShieldIcon")?.GetComponent<Image>();

        if (shieldCountText == null)
            shieldCountText = transform.Find("Content/ShieldGroup/ShieldCount")?.GetComponent<TMP_Text>();

        if (slotContainer == null)
            slotContainer = transform.Find("Content/WeaknessBar/SlotContainer") as RectTransform;
    }
}
