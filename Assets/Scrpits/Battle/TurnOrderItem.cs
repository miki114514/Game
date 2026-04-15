using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 行动队列单个图标节点。
///
/// ── Prefab 内部层级说明 ──────────────────────────────────────────
///
/// TurnOrderItem  (RectTransform, LayoutElement, TurnOrderItem)
/// │
/// ├─ DefaultGroup          [GameObject]  未选中状态根节点
/// │   ├─ Plate             Image  ← UiTX_Battle_Oder_PlateBase002
/// │   ├─ PortraitMask      RectMask2D 容器（菱形裁切）
/// │   │   └─ Portrait      Image  ← unit.portrait（头像）
/// │   ├─ Outline           Image  ← UiTX_Battle_Oder_Outline（白色细框）
/// │   ├─ ColorBadge        Image  ← UiTX_Battle_Oder_Ply（我方）
/// │   │                         / UiTX_Battle_Oder_Enm（敌方）由脚本切换
/// │   └─ StateMark         [GameObject]  异常/Break 状态标记层
/// │       ├─ DimLayer      Image  ← 纯黑半透明 alpha≈0.5（无法行动时全暗）
/// │       ├─ BreakIcon     Image  ← UiTX_Battle_Oder_Crack
/// │       ├─ SleepIcon     Image  ← UiTX_Battle_Oder_Select_Rain
/// │       └─ ConfuseIcon   Image  ← UiTX_Battle_Oder_Select_Quest
/// │
/// └─ ActiveGroup           [GameObject]  当前行动单位状态根节点（默认隐藏）
///     ├─ SelectPlate       Image  ← UiTX_Battle_Oder_Select_Mask（底板/遮罩）
///     ├─ SelectLight       Image  ← UiTX_Battle_Oder_Select_Light（背景光晕）
///     ├─ SelectCharge      Image  ← UiTX_Battle_Oder_Select_Charge01（循环动画帧）
///     ├─ PortraitMask      RectMask2D 容器（菱形裁切）
///     │   └─ Portrait      Image  ← unit.portrait
///     ├─ SelectOutline     Image  ← UiTX_Battle_Oder_Select_Outline2（发光描边）
///     ├─ ColorBadge        Image  ← UiTX_Battle_Oder_Select_Ply（我方）
///     │                         / UiTX_Battle_Oder_Select_Enm（敌方）
///     ├─ Hilight           Image  ← UiTX_Battle_Oder_Select_SelectIconHilight
///     └─ StateMark         [GameObject]  （同 DefaultGroup，复用相同子结构）
///         ├─ DimLayer      Image
///         ├─ BreakIcon     Image  ← UiTX_Battle_Oder_Crack
///         ├─ SleepIcon     Image  ← UiTX_Battle_Oder_Select_Rain
///         └─ ConfuseIcon   Image  ← UiTX_Battle_Oder_Select_Quest
///
/// ── 尺寸建议 ──────────────────────────────────────────────────────
///   TurnOrderItem  Width/Height 56×56（普通），Active 时 scale→1.2
///   所有 Image 全部关闭 Raycast Target
///   PortraitMask 容器使用 RectMask2D，Size=44×44，旋转 45°（菱形效果）
///   Portrait 内图旋转 -45° 抵消，使头像始终正向
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class TurnOrderItem : MonoBehaviour
{
    // ── 默认状态组 ──────────────────────────────
    [Header("DefaultGroup（未选中）")]
    public GameObject defaultGroup;
    public Image defaultPortrait;
    public Image defaultColorBadge;    // 我方/敌方颜色标记
    public GameObject defaultStateMark;
    public GameObject defaultDimLayer;
    public GameObject defaultBreakIcon;
    public GameObject defaultSleepIcon;
    public GameObject defaultConfuseIcon;

    // ── 激活状态组 ──────────────────────────────
    [Header("ActiveGroup（当前行动）")]
    public GameObject activeGroup;
    public Image activePortrait;
    public Image activeColorBadge;
    public GameObject activeStateMark;
    public GameObject activeDimLayer;
    public GameObject activeBreakIcon;
    public GameObject activeSleepIcon;
    public GameObject activeConfuseIcon;

    // ── 颜色配置 ────────────────────────────────
    [Header("颜色配置")]
    public Sprite plySprite;    // UiTX_Battle_Oder_Ply（我方默认）
    public Sprite enmSprite;    // UiTX_Battle_Oder_Enm（敌方默认）
    public Sprite plyActiveSprite;  // UiTX_Battle_Oder_Select_Ply
    public Sprite enmActiveSprite;  // UiTX_Battle_Oder_Select_Enm

    [Header("尺寸/排版")]
    public bool useScaleForActive = false;
    [Range(1f, 2f)] public float activeScale = 1.2f;
    public RectTransform defaultSizeReference;
    public RectTransform activeSizeReference;
    public float defaultFallbackWidth = 70f;
    public float activeFallbackWidth = 150f;

    [Header("头像自动裁剪")]
    public bool autoCropPortrait = true;
    public bool adaptiveCropByOpaqueBounds = true;
    [Range(0f, 1f)] public float alphaThreshold = 0.1f;
    [Range(0.3f, 0.9f)] public float headSearchPortion = 0.62f;
    [Range(0f, 0.6f)] public float headTopPadding = 0.20f;
    [Range(0f, 0.6f)] public float headBottomPadding = 0.08f;
    [Range(0f, 0.6f)] public float headSidePadding = 0.16f;
    [Range(0.2f, 1.0f)] public float minSizeRelativeToBodyWidth = 0.60f;
    [Range(0.2f, 1.0f)] public float minSizeRelativeToBodyHeight = 0.35f;
    [Range(0.2f, 0.8f)] public float adaptiveHeadPortion = 0.45f;
    [Range(0.5f, 1.6f)] public float adaptiveWidthFactor = 1.0f;
    [Range(0.8f, 2.0f)] public float adaptiveHeightFactor = 1.15f;
    [Range(-0.3f, 0.3f)] public float adaptiveVerticalOffset = -0.03f;

    [Header("手动裁剪（自适应失败时回退）")]
    [Range(0f, 1f)] public float cropCenterX = 0.5f;
    [Range(0f, 1f)] public float cropCenterY = 0.72f;
    [Range(0.05f, 1f)] public float cropWidth = 0.48f;
    [Range(0.05f, 1f)] public float cropHeight = 0.48f;
    public Sprite defaultMaskSprite; // UiTX_Battle_Oder_Mask
    public Sprite activeMaskSprite;  // UiTX_Battle_Oder_Select_Mask

    [Header("裁剪调试")]
    public bool enableCropDebugLog = false;
    [SerializeField, TextArea(2, 5)] private string lastCropDebugInfo;

    // ── 当前绑定单位 ─────────────────────────────
    private BattleUnit _unit;
    private bool _isActive;
    private readonly Dictionary<string, Sprite> _portraitCache = new Dictionary<string, Sprite>();
    private readonly HashSet<string> _debugLoggedKeys = new HashSet<string>();
    private string _lastCropMode = "unknown";
    private string _lastCropReason = string.Empty;
    private Rect _lastCropRect;

    // ==========================================================
    // 公开方法：由 TurnOrderUIManager 驱动
    // ==========================================================

    /// <summary>
    /// 绑定单位并设置显示状态。
    /// isActive = true  →  ActiveGroup 显示（当前回合第一位）
    /// isActive = false →  DefaultGroup 显示
    /// </summary>
    public void SetUnit(BattleUnit unit, bool isActive)
    {
        _unit     = unit;
        _isActive = isActive;

        // 切换主要图层
        defaultGroup.SetActive(!isActive);
        activeGroup.SetActive(isActive);

        // 头像：传入立绘，内部自动裁剪头像区域，并启用菱形遮罩
        ApplyPortrait(unit.tachie);

        // 颜色标记（我方 / 敌方）
        ApplyColorBadge(unit, isActive);

        // 缩放：若激活组本身已做大，可关闭该选项避免二次放大
        transform.localScale = (isActive && useScaleForActive)
            ? Vector3.one * activeScale
            : Vector3.one;

        // 状态覆盖
        RefreshStateMark();
    }

    /// <summary>行动后刷新状态标记（不重新绑定头像，纯刷新 Mark 层）</summary>
    public void RefreshStateMark()
    {
        if (_unit == null) return;

        bool isBreak   = _unit.isBreak;
        bool isSleep   = _unit.HasStatus(StatusEffectType.Sleep);
        bool isFreeze  = _unit.HasStatus(StatusEffectType.Freeze);
        bool isConfuse = _unit.IsConfused;
        bool cannotAct = isBreak || isSleep || isFreeze;

        // ── DefaultGroup StateMark ──
        if (defaultStateMark != null)
        {
            bool anyMark = cannotAct || isConfuse;
            defaultStateMark.SetActive(anyMark);
            SafeSetActive(defaultDimLayer,    cannotAct);
            SafeSetActive(defaultBreakIcon,   isBreak);
            SafeSetActive(defaultSleepIcon,   isSleep || isFreeze);
            SafeSetActive(defaultConfuseIcon, isConfuse && !cannotAct);
        }

        // ── ActiveGroup StateMark ──
        if (activeStateMark != null)
        {
            bool anyMark = cannotAct || isConfuse;
            activeStateMark.SetActive(anyMark);
            SafeSetActive(activeDimLayer,    cannotAct);
            SafeSetActive(activeBreakIcon,   isBreak);
            SafeSetActive(activeSleepIcon,   isSleep || isFreeze);
            SafeSetActive(activeConfuseIcon, isConfuse && !cannotAct);
        }
    }

    // ==========================================================
    // 私有帮助
    // ==========================================================

    void ApplyColorBadge(BattleUnit unit, bool isActive)
    {
        bool isPlayer = unit.unitType == UnitType.Player;

        if (defaultColorBadge != null && !isActive)
            defaultColorBadge.sprite = isPlayer ? plySprite : enmSprite;

        if (activeColorBadge != null && isActive)
            activeColorBadge.sprite = isPlayer ? plyActiveSprite : enmActiveSprite;
    }

    void ApplyPortrait(Sprite source)
    {
        Sprite portraitSprite = source;
        if (source != null && autoCropPortrait)
            portraitSprite = GetOrCreatePortraitCrop(source);

        if (defaultPortrait != null)
        {
            defaultPortrait.sprite = portraitSprite;
            defaultPortrait.preserveAspect = true;
            defaultPortrait.maskable = true;
            SetupDiamondMask(defaultPortrait, defaultMaskSprite);
        }

        if (activePortrait != null)
        {
            activePortrait.sprite = portraitSprite;
            activePortrait.preserveAspect = true;
            activePortrait.maskable = true;
            SetupDiamondMask(activePortrait, activeMaskSprite != null ? activeMaskSprite : defaultMaskSprite);
        }

        UpdateCropDebugInfo(source, portraitSprite);
    }

    Sprite GetOrCreatePortraitCrop(Sprite source)
    {
        if (source == null || source.texture == null)
            return source;

        string key = source.GetInstanceID() + "_"
            + adaptiveCropByOpaqueBounds.ToString() + "_"
            + alphaThreshold.ToString("F3") + "_"
            + headSearchPortion.ToString("F3") + "_"
            + headTopPadding.ToString("F3") + "_"
            + headBottomPadding.ToString("F3") + "_"
            + headSidePadding.ToString("F3") + "_"
            + minSizeRelativeToBodyWidth.ToString("F3") + "_"
            + minSizeRelativeToBodyHeight.ToString("F3") + "_"
            + adaptiveHeadPortion.ToString("F3") + "_"
            + adaptiveWidthFactor.ToString("F3") + "_"
            + adaptiveHeightFactor.ToString("F3") + "_"
            + adaptiveVerticalOffset.ToString("F3") + "_"
            + cropCenterX.ToString("F3") + "_"
            + cropCenterY.ToString("F3") + "_"
            + cropWidth.ToString("F3") + "_"
            + cropHeight.ToString("F3");

        if (_portraitCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Rect cropRect = BuildAdaptiveCropRect(source);
        _lastCropRect = cropRect;

        Sprite cropped = Sprite.Create(
            source.texture,
            cropRect,
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            Vector4.zero,
            false);

        _portraitCache[key] = cropped;
        return cropped;
    }

    Rect BuildAdaptiveCropRect(Sprite source)
    {
        Rect src = source.rect;

        if (adaptiveCropByOpaqueBounds && TryGetOpaqueBounds(source, out Rect opaque))
        {
            if (TryBuildHeadPriorityCropRect(source, opaque, out Rect headPriorityCrop))
            {
                _lastCropMode = "adaptive";
                _lastCropReason = "head-priority";
                return headPriorityCrop;
            }

            float bodyW = Mathf.Max(1f, opaque.width);
            float bodyH = Mathf.Max(1f, opaque.height);

            float headH = bodyH * Mathf.Clamp(adaptiveHeadPortion, 0.2f, 0.8f);
            float size = Mathf.Max(
                bodyW * Mathf.Clamp(adaptiveWidthFactor, 0.5f, 1.6f),
                headH * Mathf.Clamp(adaptiveHeightFactor, 0.8f, 2.0f));

            float centerX = opaque.x + bodyW * 0.5f;
            float centerY = opaque.y + bodyH * (1f - adaptiveHeadPortion * 0.5f + adaptiveVerticalOffset);

            _lastCropMode = "adaptive";
            _lastCropReason = "body-fallback";

            return ClampRectIntoSource(src, new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size));
        }

        // 回退到手动归一化裁剪参数
        float w = src.width * Mathf.Clamp(cropWidth, 0.05f, 1f);
        float h = src.height * Mathf.Clamp(cropHeight, 0.05f, 1f);
        float cx = src.x + src.width * Mathf.Clamp01(cropCenterX);
        float cy = src.y + src.height * Mathf.Clamp01(cropCenterY);
        _lastCropMode = "manual-fallback";
        _lastCropReason = "opaque-bounds-unavailable";
        return ClampRectIntoSource(src, new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h));
    }

    bool TryBuildHeadPriorityCropRect(Sprite source, Rect bodyOpaque, out Rect result)
    {
        result = default;

        float portion = Mathf.Clamp(headSearchPortion, 0.3f, 0.9f);
        float headRegionHeight = bodyOpaque.height * portion;
        Rect headSearchRect = new Rect(
            bodyOpaque.x,
            bodyOpaque.yMax - headRegionHeight,
            bodyOpaque.width,
            headRegionHeight);

        if (!TryGetOpaqueBoundsInRect(source, headSearchRect, out Rect headOpaque))
        {
            _lastCropReason = "head-search-empty";
            return false;
        }

        float headW = Mathf.Max(1f, headOpaque.width);
        float headH = Mathf.Max(1f, headOpaque.height);
        float bodyW = Mathf.Max(1f, bodyOpaque.width);
        float bodyH = Mathf.Max(1f, bodyOpaque.height);

        float paddedW = headW * (1f + Mathf.Clamp(headSidePadding, 0f, 0.6f) * 2f);
        float paddedH = headH * (1f + Mathf.Clamp(headTopPadding, 0f, 0.6f) + Mathf.Clamp(headBottomPadding, 0f, 0.6f));

        float minSizeW = bodyW * Mathf.Clamp(minSizeRelativeToBodyWidth, 0.2f, 1f);
        float minSizeH = bodyH * Mathf.Clamp(minSizeRelativeToBodyHeight, 0.2f, 1f);
        float size = Mathf.Max(paddedW, paddedH, minSizeW, minSizeH);

        float centerX = headOpaque.center.x;

        // 以头顶上留白为优先，保证视觉上“头顶有空气感”
        float topY = headOpaque.yMax + headH * Mathf.Clamp(headTopPadding, 0f, 0.6f);
        float centerY = topY - size * 0.5f;

        result = ClampRectIntoSource(source.rect, new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size));
        return true;
    }

    Rect ClampRectIntoSource(Rect src, Rect r)
    {
        float w = Mathf.Min(src.width, Mathf.Max(1f, r.width));
        float h = Mathf.Min(src.height, Mathf.Max(1f, r.height));
        float x = Mathf.Clamp(r.x, src.x, src.xMax - w);
        float y = Mathf.Clamp(r.y, src.y, src.yMax - h);
        return new Rect(x, y, w, h);
    }

    bool TryGetOpaqueBounds(Sprite source, out Rect bounds)
    {
        bounds = default;

        Texture2D tex = source.texture;
        if (tex == null || !tex.isReadable)
        {
            _lastCropReason = "texture-not-readable";
            return false;
        }

        Rect src = source.rect;
        int x0 = Mathf.FloorToInt(src.x);
        int y0 = Mathf.FloorToInt(src.y);
        int w = Mathf.FloorToInt(src.width);
        int h = Mathf.FloorToInt(src.height);
        if (w <= 0 || h <= 0)
        {
            _lastCropReason = "invalid-source-rect";
            return false;
        }

        Color[] px;
        try
        {
            px = tex.GetPixels(x0, y0, w, h);
        }
        catch
        {
            _lastCropReason = "getpixels-failed";
            return false;
        }

        int minX = w;
        int minY = h;
        int maxX = -1;
        int maxY = -1;
        float threshold = Mathf.Clamp01(alphaThreshold);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (px[row + x].a < threshold) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            _lastCropReason = "no-opaque-pixels";
            return false;
        }

        bounds = new Rect(
            src.x + minX,
            src.y + minY,
            (maxX - minX + 1),
            (maxY - minY + 1));
        return true;
    }

    bool TryGetOpaqueBoundsInRect(Sprite source, Rect searchRect, out Rect bounds)
    {
        bounds = default;
        Rect src = source.rect;
        Rect clipped = Rect.MinMaxRect(
            Mathf.Max(src.xMin, searchRect.xMin),
            Mathf.Max(src.yMin, searchRect.yMin),
            Mathf.Min(src.xMax, searchRect.xMax),
            Mathf.Min(src.yMax, searchRect.yMax));

        if (clipped.width <= 0f || clipped.height <= 0f)
            return false;

        Texture2D tex = source.texture;
        if (tex == null || !tex.isReadable)
            return false;

        int x0 = Mathf.FloorToInt(clipped.x);
        int y0 = Mathf.FloorToInt(clipped.y);
        int w = Mathf.FloorToInt(clipped.width);
        int h = Mathf.FloorToInt(clipped.height);
        if (w <= 0 || h <= 0)
            return false;

        Color[] px;
        try
        {
            px = tex.GetPixels(x0, y0, w, h);
        }
        catch
        {
            return false;
        }

        int minX = w;
        int minY = h;
        int maxX = -1;
        int maxY = -1;
        float threshold = Mathf.Clamp01(alphaThreshold);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (px[row + x].a < threshold) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return false;

        bounds = new Rect(
            clipped.x + minX,
            clipped.y + minY,
            (maxX - minX + 1),
            (maxY - minY + 1));
        return true;
    }

    void UpdateCropDebugInfo(Sprite source, Sprite result)
    {
        if (source == null)
        {
            lastCropDebugInfo = "source=null";
            return;
        }

        var sb = new StringBuilder();
        sb.Append("mode=").Append(_lastCropMode)
          .Append(" | reason=").Append(_lastCropReason)
          .Append(" | src=").Append(source.name)
          .Append(" rect(").Append(source.rect.x.ToString("F0")).Append(",")
          .Append(source.rect.y.ToString("F0")).Append(",")
          .Append(source.rect.width.ToString("F0")).Append(",")
          .Append(source.rect.height.ToString("F0")).Append(")")
          .Append(" | crop(").Append(_lastCropRect.x.ToString("F1")).Append(",")
          .Append(_lastCropRect.y.ToString("F1")).Append(",")
          .Append(_lastCropRect.width.ToString("F1")).Append(",")
          .Append(_lastCropRect.height.ToString("F1")).Append(")");

        if (result != null)
            sb.Append(" | out=").Append(result.rect.width.ToString("F0")).Append("x").Append(result.rect.height.ToString("F0"));

        lastCropDebugInfo = sb.ToString();

        if (!enableCropDebugLog)
            return;

        string debugKey = source.GetInstanceID() + "_" + _lastCropMode + "_" + _lastCropRect;
        if (_debugLoggedKeys.Add(debugKey))
            Debug.Log("[TurnOrderItem] CropDebug " + lastCropDebugInfo, this);
    }

    void SetupDiamondMask(Image portraitImage, Sprite maskSprite)
    {
        if (portraitImage == null || portraitImage.transform.parent == null)
            return;

        Transform maskRoot = portraitImage.transform.parent;

        RectMask2D rectMask = maskRoot.GetComponent<RectMask2D>();
        if (rectMask != null)
            Destroy(rectMask);

        Image maskImage = maskRoot.GetComponent<Image>();
        if (maskImage == null)
            maskImage = maskRoot.gameObject.AddComponent<Image>();

        if (maskSprite != null)
            maskImage.sprite = maskSprite;

        maskImage.type = Image.Type.Simple;
        maskImage.preserveAspect = true;
        maskImage.raycastTarget = false;

        Mask mask = maskRoot.GetComponent<Mask>();
        if (mask == null)
            mask = maskRoot.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
    }

    static void SafeSetActive(GameObject go, bool state)
    {
        if (go != null) go.SetActive(state);
    }

    /// <summary>
    /// 返回当前节点用于“横向贴边计算”的视觉宽度。
    /// 优先取 SizeReference 的 Rect 宽度，其次取 Group 根节点宽度，最后回退默认值。
    /// </summary>
    public float GetVisualWidth(bool asActive, float fallbackWidth)
    {
        float width = asActive ? activeFallbackWidth : defaultFallbackWidth;

        RectTransform refRt = asActive ? activeSizeReference : defaultSizeReference;
        if (refRt == null)
        {
            GameObject group = asActive ? activeGroup : defaultGroup;
            if (group != null) refRt = group.GetComponent<RectTransform>();
        }

        if (refRt != null && refRt.rect.width > 0.1f)
            width = refRt.rect.width;
        else
            width = fallbackWidth > 0f ? fallbackWidth : width;

        if (asActive && useScaleForActive)
            width *= activeScale;

        return Mathf.Max(1f, width);
    }
}
