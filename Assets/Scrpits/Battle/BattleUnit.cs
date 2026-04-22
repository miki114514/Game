using UnityEngine;
using System;
using System.Collections.Generic;

public enum UnitType { Player, Enemy }
public enum DamageType { Physical, Elemental }

public enum WeaponType
{
    None,
    Sword,
    Lance,
    Dagger,
    Axe,
    Bow,
    Staff
}

public enum ElementType
{
    None,
    Fire,
    Ice,
    Thunder,
    Wind,
    Light,
    Dark
}

public enum BattleIdleAnimationSource
{
    SpriteFrames,
    AnimatorState,
    Auto
}

/// <summary>
/// 扁平化弱点枚举：供敌人弱点列表与 Break UI 映射使用。
/// 保留 Strike / Pierce 作为旧数据兼容项。
/// </summary>
public enum AttackType
{
    None = 0,
    Sword = 1,
    Fire = 2,
    Ice = 3,
    Thunder = 4,
    Strike = 5,
    Pierce = 6,
    Lance = 7,
    Dagger = 8,
    Axe = 9,
    Bow = 10,
    Staff = 11,
    Wind = 12,
    Light = 13,
    Dark = 14
}

[Serializable]
public class WeaponAttackEntry
{
    public WeaponType weaponType = WeaponType.None;
    public AttackType attackType = AttackType.None; // 旧字段兼容
    public int attackPower = 0;

    public WeaponType ResolveWeaponType()
    {
        if (weaponType != WeaponType.None)
            return weaponType;

        return BattleFormula.ToWeaponType(attackType);
    }
}

public class BattleUnit : MonoBehaviour
{
    public const int MaxLevel = 60;

    [Header("基础属性")]
    public string unitName;
    public UnitType unitType;

    [Header("头像")]
    [Tooltip("角色立绘素材，将自动裁剪头部区域生成 Portrait 头像")]
    public Sprite tachie;

    [Header("战斗待机动画")]
    [Tooltip("选择待机动画来源：SpriteFrames=逐帧切图，AnimatorState=播放 Animator 状态，Auto=优先 Animator，失败回退逐帧/静态图")]
    public BattleIdleAnimationSource idleAnimationSource = BattleIdleAnimationSource.SpriteFrames;
    [Tooltip("不指定时会自动从自身或子物体查找 Animator")]
    public Animator battleAnimator;
    [Tooltip("Animator 待机状态名，支持完整路径，如 Base Layer.Idle")]
    public string idleAnimationStateName;
    public bool randomizeAnimatorIdleStartTime = true;

    [Tooltip("留空时默认使用挂载的 SpriteRenderer 当前图片")]
    public List<Sprite> idleAnimationFrames = new List<Sprite>();
    [Min(0.01f)] public float idleAnimationFrameInterval = 0.16f;
    public bool idleAnimationLoop = true;
    public bool randomizeIdleStartFrame = true;
    [Tooltip("不指定时会自动从自身或子物体查找 SpriteRenderer")]
    public SpriteRenderer battleSpriteRenderer;

    [Header("战斗入场动画")]
    public bool playBattleEnterAnimation = true;
    [Tooltip("Animator 入场状态名，支持完整路径，如 Base Layer.BU_Shulk_Enter")]
    public string enterAnimationStateName;
    public bool randomizeEnterAnimationStartTime = false;
    [Tooltip("勾选后会等待入场动画首轮播放完毕再切换到待机")]
    public bool waitForEnterAnimationToFinish = true;
    [Min(0f)] public float enterAnimationFallbackDuration = 0.35f;
    public bool playBattleEnterMove = true;
    [Min(0f)] public float enterMoveDistance = 3.5f;
    [Min(0.01f)] public float enterMoveDuration = 0.35f;

    [Header("头像裁剪参数")]
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
    [Range(0f, 1f)] public float cropCenterX = 0.5f;
    [Range(0f, 1f)] public float cropCenterY = 0.72f;
    [Range(0.05f, 1f)] public float cropWidth = 0.48f;
    [Range(0.05f, 1f)] public float cropHeight = 0.48f;

    [System.NonSerialized] private Sprite _generatedPortrait;

    /// <summary>由立绘（tachie）自动裁剪头部生成的头像，供行动队列与角色数据结构读取</summary>
    public Sprite portrait
    {
        get
        {
            if (_generatedPortrait != null) return _generatedPortrait;
            if (tachie == null) return null;
            _generatedPortrait = GeneratePortraitCrop(tachie);
            return _generatedPortrait;
        }
    }

    [Range(1, MaxLevel)]
    public int level = 1;

    public int maxHP = 100;
    public int currentHP;
    public int maxSP = 50;
    public int currentSP;

    [Header("BP系统")]
    public int maxBP = BattleFormula.DefaultMaxBP;
    [SerializeField] private int currentBP = 0;
    public int startBattleBP = 0;
    public int bpRecoveryPerTurn = 1;

    public int physicalAttack = 20;
    public int magicAttack = 15;
    public int physicalDefense = 10;
    public int magicDefense = 8;

    public int accuracy = 95;    // 命中率 %
    public int speed = 10;       // 行动速度
    public int critRate = 10;    // 暴击 %
    public int evasion = 5;      // 回避 %

    [Header("普通攻击类型")]
    public DamageType normalAttackDamageType = DamageType.Physical;
    public WeaponType normalAttackWeaponType = WeaponType.Sword;
    public ElementType normalAttackElementType = ElementType.None;
    public AttackType normalAttackType = AttackType.Sword; // 旧字段兼容

    [Header("武器攻击力")]
    public List<WeaponAttackEntry> weaponAttackEntries = new List<WeaponAttackEntry>();

    [Header("玩家成长")]
    public int currentExp = 0;
    public int expToNextLevel = 100;

    [Header("职业熟练度（JP）")]
    [Min(0)] public int currentJP = 0;

    [Header("敌人护盾")]
    public int maxShield = 0;
    public int currentShield = 0;
    public List<AttackType> weaknessTypes = new List<AttackType>();
    public List<StatusEffectType> statusImmunities = new List<StatusEffectType>();

    [Header("战技倍率")]
    public float artsAbilityMultiplier = 1f;
    public float forcedDamageReductionMultiplier = 1f;

    [Header("职业")]
    public CharacterClassDefinition classDefinition;

    [Header("技能管理")]
    public List<Skill> artsList = new List<Skill>();   // 战技列表（Arts）
    public List<Skill> skillList = new List<Skill>();  // 角色技能列表（Skill）

    [Header("装备栏")]
    public WeaponData    equippedWeapon;
    public ArmorData     equippedHead;
    public ArmorData     equippedBody;
    public AccessoryData[] equippedAccessories = new AccessoryData[2];

    // 装备提供的 HP / SP 上限加成缓存（由 RecalculateEquipmentBonuses 计算）
    private int equipmentHPBonus;
    private int equipmentSPBonus;

    public event Action<int, int> OnHPChanged;
    public event Action<int, int> OnSPChanged;
    public event Action<int, int> OnBPChanged;
    public event Action<int, int> OnEXPChanged;
    public event Action<int> OnJPChanged;
    public event Action<int, int> OnShieldChanged;
    public event Action<AttackType> OnWeaknessRevealed;
    public event Action OnWeaknessStateChanged;

    private readonly HashSet<AttackType> revealedWeaknessTypes = new HashSet<AttackType>();
    private int breakSkipTurnCount = 0;
    private bool isDefending = false;
    private bool hasEnteredTurnOnce = false;
    private Sprite defaultIdleSprite;
    private int currentIdleFrameIndex = -1;
    private float idleAnimationElapsed = 0f;
    private bool hasIdleAnimationFrames = false;
    private bool isUsingAnimatorIdle = false;
    private bool isPlayingEnterAnimation = false;
    private bool hasPlayedBattlePresentation = false;
    private Vector3 spawnPoint;
    private Vector3 battlePresentationAnchorPosition;
    private Coroutine battlePresentationCoroutine;

    public bool IsDefending => isDefending;
    public int CurrentBP => currentBP;
    public int MaxBP => maxBP;
    public float LevelMultiplier => BattleFormula.GetLevelMultiplier(level);

    // ── 装备加成后有效属性上限 ──
    /// <summary>含装备 HP 加成的最大 HP</summary>
    public int EffectiveMaxHP => maxHP + equipmentHPBonus;
    /// <summary>含装备 SP 加成的最大 SP</summary>
    public int EffectiveMaxSP => maxSP + equipmentSPBonus;

    // ── 含装备加成的最终战斗属性 ──
    /// <summary>最终速度 = 基础 + 装备合计</summary>
    public int FinalSpeed    => speed    + GetEquipmentTotalSpeed();
    /// <summary>最终命中 = 基础 + 装备合计</summary>
    public int FinalAccuracy => accuracy + GetEquipmentTotalAccuracy();
    /// <summary>最终暴击 = 基础 + 装备合计</summary>
    public int FinalCritRate => critRate + GetEquipmentTotalCrit();

#if UNITY_EDITOR
    void OnValidate() { _generatedPortrait = null; }
#endif

    void Awake()
    {
        _generatedPortrait = null;
        InitializeBattleState();
        EnsureAnimationReferences();
        spawnPoint = transform.position;
        battlePresentationAnchorPosition = transform.position;
    }

    void Update()
    {
        if (isPlayingEnterAnimation)
            return;

        TickIdleAnimation();
    }

    void EnsureAnimationReferences()
    {
        if (battleAnimator == null)
            battleAnimator = GetComponentInChildren<Animator>();

        if (battleSpriteRenderer == null)
            battleSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    System.Collections.IEnumerator PlayEnterAnimationThenIdleRoutine()
    {
        bool hasEnterAnimator = TryPlayEnterAnimation(out float enterAnimDuration);
        bool hasEnterMove = TrySetupEnterMove(out Vector3 moveStartPos, out Vector3 moveTargetPos, out float moveDuration);

        float waitDuration = 0f;
        if (waitForEnterAnimationToFinish && hasEnterAnimator)
            waitDuration = Mathf.Max(waitDuration, enterAnimDuration);
        if (hasEnterMove)
            waitDuration = Mathf.Max(waitDuration, moveDuration);

        if (hasEnterMove)
            transform.position = moveStartPos;

        if (waitDuration > 0f)
        {
            isPlayingEnterAnimation = true;

            float elapsed = 0f;
            // Additive scene loading can produce a very large first-frame deltaTime.
            // Yield once so the unit is rendered at the entry start position before interpolation begins.
            yield return null;

            while (elapsed < waitDuration)
            {
                // Clamp time step to avoid jumping to target in one frame after scene transitions.
                float step = Mathf.Min(Time.deltaTime, 0.05f);
                if (step <= 0f)
                {
                    yield return null;
                    continue;
                }

                elapsed += step;
                if (hasEnterMove)
                {
                    float t = Mathf.Clamp01(elapsed / moveDuration);
                    transform.position = Vector3.Lerp(moveStartPos, moveTargetPos, t);
                }

                yield return null;
            }

            if (hasEnterMove)
                transform.position = moveTargetPos;

            isPlayingEnterAnimation = false;
        }
        else if (hasEnterMove)
        {
            transform.position = moveTargetPos;
        }

        InitializeIdleAnimation();
    }

    public void PlayBattlePresentation(bool forceRestart)
    {
        if (!isActiveAndEnabled)
            return;

        EnsureAnimationReferences();

        if (forceRestart)
        {
            if (battlePresentationCoroutine != null)
            {
                StopCoroutine(battlePresentationCoroutine);
                battlePresentationCoroutine = null;
            }

            hasPlayedBattlePresentation = false;
            isPlayingEnterAnimation = false;
            // 重启时先将单位归位到出生点，避免动画中途打断时捕获到偏移位置
            transform.position = spawnPoint;
            battlePresentationAnchorPosition = spawnPoint;
        }

        if (hasPlayedBattlePresentation)
            return;

        battlePresentationCoroutine = StartCoroutine(PlayBattlePresentationRoutine());
    }

    System.Collections.IEnumerator PlayBattlePresentationRoutine()
    {
        hasPlayedBattlePresentation = true;
        yield return PlayEnterAnimationThenIdleRoutine();
        battlePresentationCoroutine = null;
    }

    void InitializeIdleAnimation()
    {
        isUsingAnimatorIdle = false;
        EnsureAnimationReferences();

        if (TryPlayAnimatorIdle())
        {
            isUsingAnimatorIdle = true;
            return;
        }

        if (battleSpriteRenderer == null)
            return;

        defaultIdleSprite = battleSpriteRenderer.sprite;
        hasIdleAnimationFrames = HasValidIdleAnimationFrames();
        currentIdleFrameIndex = -1;
        idleAnimationElapsed = 0f;

        if (!hasIdleAnimationFrames)
        {
            battleSpriteRenderer.sprite = defaultIdleSprite;
            return;
        }

        int firstIndex = randomizeIdleStartFrame
            ? GetRandomValidIdleFrameIndex()
            : GetFirstValidIdleFrameIndex();

        if (firstIndex < 0)
        {
            hasIdleAnimationFrames = false;
            battleSpriteRenderer.sprite = defaultIdleSprite;
            return;
        }

        ApplyIdleFrame(firstIndex);
    }

    void TickIdleAnimation()
    {
        if (isUsingAnimatorIdle || battleSpriteRenderer == null || !hasIdleAnimationFrames)
            return;

        float frameInterval = Mathf.Max(0.01f, idleAnimationFrameInterval);
        idleAnimationElapsed += Time.deltaTime;
        if (idleAnimationElapsed < frameInterval)
            return;

        idleAnimationElapsed -= frameInterval;

        int nextIndex = idleAnimationLoop
            ? GetNextValidIdleFrameIndex(currentIdleFrameIndex, true)
            : GetNextValidIdleFrameIndex(currentIdleFrameIndex, false);

        if (nextIndex >= 0)
            ApplyIdleFrame(nextIndex);
    }

    bool HasValidIdleAnimationFrames()
    {
        if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            return false;

        for (int i = 0; i < idleAnimationFrames.Count; i++)
        {
            if (idleAnimationFrames[i] != null)
                return true;
        }

        return false;
    }

    int GetFirstValidIdleFrameIndex()
    {
        if (idleAnimationFrames == null)
            return -1;

        for (int i = 0; i < idleAnimationFrames.Count; i++)
        {
            if (idleAnimationFrames[i] != null)
                return i;
        }

        return -1;
    }

    int GetRandomValidIdleFrameIndex()
    {
        if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            return -1;

        List<int> validIndices = new List<int>();
        for (int i = 0; i < idleAnimationFrames.Count; i++)
        {
            if (idleAnimationFrames[i] != null)
                validIndices.Add(i);
        }

        if (validIndices.Count == 0)
            return -1;

        int picked = UnityEngine.Random.Range(0, validIndices.Count);
        return validIndices[picked];
    }

    int GetNextValidIdleFrameIndex(int currentIndex, bool wrap)
    {
        if (idleAnimationFrames == null || idleAnimationFrames.Count == 0)
            return -1;

        for (int i = currentIndex + 1; i < idleAnimationFrames.Count; i++)
        {
            if (idleAnimationFrames[i] != null)
                return i;
        }

        if (!wrap)
            return -1;

        for (int i = 0; i <= currentIndex && i < idleAnimationFrames.Count; i++)
        {
            if (idleAnimationFrames[i] != null)
                return i;
        }

        return -1;
    }

    void ApplyIdleFrame(int frameIndex)
    {
        if (battleSpriteRenderer == null || idleAnimationFrames == null)
            return;

        if (frameIndex < 0 || frameIndex >= idleAnimationFrames.Count)
            return;

        Sprite frame = idleAnimationFrames[frameIndex];
        if (frame == null)
            return;

        battleSpriteRenderer.sprite = frame;
        currentIdleFrameIndex = frameIndex;
    }

    bool TryPlayEnterAnimation(out float duration)
    {
        duration = 0f;

        if (!playBattleEnterAnimation)
            return false;

        if (battleAnimator == null)
        {
            Debug.LogWarning($"[BattleUnit] {unitName} 未找到 Animator，跳过入场动画。", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(enterAnimationStateName))
        {
            Debug.LogWarning($"[BattleUnit] {unitName} 未配置 Enter State Name，跳过入场动画。", this);
            return false;
        }

        string trimmedStateName = enterAnimationStateName.Trim();
        if (!HasAnimatorState(trimmedStateName))
        {
            Debug.LogWarning($"[BattleUnit] {unitName} 找不到入场状态 `{trimmedStateName}`，跳过入场动画。", this);
            return false;
        }

        float startTime = randomizeEnterAnimationStartTime ? UnityEngine.Random.value : 0f;
        battleAnimator.Play(trimmedStateName, -1, startTime);
        battleAnimator.Update(0f);

        AnimatorStateInfo stateInfo = battleAnimator.GetCurrentAnimatorStateInfo(0);
        duration = Mathf.Max(enterAnimationFallbackDuration, stateInfo.length);
        return true;
    }

    bool TrySetupEnterMove(out Vector3 startPos, out Vector3 targetPos, out float duration)
    {
        targetPos = spawnPoint;   // 始终以 Awake 时记录的出生点为落点
        startPos = targetPos;
        duration = Mathf.Max(0.01f, enterMoveDuration);

        if (!playBattleEnterAnimation || !playBattleEnterMove)
            return false;

        float distance = Mathf.Max(0f, enterMoveDistance);
        if (distance <= 0f)
            return false;

        Vector3 dir = unitType == UnitType.Player ? Vector3.right : Vector3.left;
        startPos = targetPos + dir * distance;
        return true;
    }

    bool TryPlayAnimatorIdle()
    {
        if (!ShouldUseAnimatorIdle())
            return false;

        if (battleAnimator == null)
        {
            Debug.LogWarning($"[BattleUnit] {unitName} 未找到 Animator，待机动画回退到 Sprite。", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(idleAnimationStateName))
        {
            Debug.LogWarning($"[BattleUnit] {unitName} 未配置 Idle State Name，待机动画回退到 Sprite。", this);
            return false;
        }

        string trimmedStateName = idleAnimationStateName.Trim();
        if (!HasAnimatorState(trimmedStateName))
        {
            Debug.LogWarning($"[BattleUnit] {unitName} 找不到待机状态 `{trimmedStateName}`，待机动画回退到 Sprite。", this);
            return false;
        }

        float startTime = randomizeAnimatorIdleStartTime ? UnityEngine.Random.value : 0f;
        battleAnimator.Play(trimmedStateName, -1, startTime);
        battleAnimator.Update(0f);
        return true;
    }

    bool ShouldUseAnimatorIdle()
    {
        if (idleAnimationSource == BattleIdleAnimationSource.AnimatorState)
            return true;

        if (idleAnimationSource == BattleIdleAnimationSource.Auto)
            return true;

        return false;
    }

    bool HasAnimatorState(string stateName)
    {
        if (battleAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int fullHash = Animator.StringToHash(stateName);
        for (int i = 0; i < battleAnimator.layerCount; i++)
        {
            if (battleAnimator.HasState(i, fullHash))
                return true;
        }

        for (int i = 0; i < battleAnimator.layerCount; i++)
        {
            string layerQualifiedName = battleAnimator.GetLayerName(i) + "." + stateName;
            int hash = Animator.StringToHash(layerQualifiedName);
            if (battleAnimator.HasState(i, hash))
                return true;
        }

        return false;
    }

    public void InitializeBattleState()
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        maxBP = Mathf.Max(0, maxBP);
        startBattleBP = Mathf.Clamp(startBattleBP, 0, maxBP);
        bpRecoveryPerTurn = Mathf.Max(0, bpRecoveryPerTurn);
        RecalculateEquipmentBonuses();
        currentHP = EffectiveMaxHP;
        currentSP = EffectiveMaxSP;
        ResetBP();
        isBreak = false;
        breakSkipTurnCount = 0;
        ResetRevealedWeaknesses();
        ResetShield();
        isDefending = false;
        forcedDamageReductionMultiplier = Mathf.Max(0f, forcedDamageReductionMultiplier);
        SanitizeSkillListsByClass();
    }

    // =========================
    // 受伤
    // =========================
    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);

        Debug.Log($"{unitName} 受到伤害: {damage}");

        OnHPChanged?.Invoke(currentHP, EffectiveMaxHP);
    }

    // =========================
    // 消耗SP
    // =========================
    public void UseSP(int cost)
    {
        currentSP -= cost;
        currentSP = Mathf.Max(currentSP, 0);

        OnSPChanged?.Invoke(currentSP, EffectiveMaxSP);
    }

    public bool HasEnoughSP(int cost)
    {
        int safeCost = Mathf.Max(0, cost);
        return currentSP >= safeCost;
    }

    // =========================
    // BP 系统
    // =========================
    public void ResetBP()
    {
        currentBP = Mathf.Clamp(startBattleBP, 0, maxBP);
        hasEnteredTurnOnce = false;
        OnBPChanged?.Invoke(currentBP, maxBP);
    }

    public void HandleTurnStartBP()
    {
        if (!hasEnteredTurnOnce)
        {
            hasEnteredTurnOnce = true;
            OnBPChanged?.Invoke(currentBP, maxBP);
            return;
        }

        GainBP(bpRecoveryPerTurn);
    }

    public void GainBP(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        int oldBP = currentBP;
        currentBP = Mathf.Clamp(currentBP + safeAmount, 0, maxBP);
        hasEnteredTurnOnce = true;

        if (currentBP != oldBP)
            Debug.Log($"[BP] {unitName} 回复了 {currentBP - oldBP} BP，当前 {currentBP}/{maxBP}");

        OnBPChanged?.Invoke(currentBP, maxBP);
    }

    public bool SpendBP(int amount)
    {
        int spend = Mathf.Clamp(amount, 0, currentBP);

        if (spend <= 0)
        {
            OnBPChanged?.Invoke(currentBP, maxBP);
            return amount <= 0;
        }

        currentBP -= spend;
        OnBPChanged?.Invoke(currentBP, maxBP);
        Debug.Log($"[BP] {unitName} 消耗了 {spend} BP，当前 {currentBP}/{maxBP}");
        return spend == amount;
    }

    public int GetMaxAvailableBoostLevel()
    {
        return Mathf.Min(BattleFormula.MaxBoostLevel, currentBP);
    }

    public int ClampBoostLevel(int requestedBoostLevel)
    {
        return Mathf.Clamp(requestedBoostLevel, 0, GetMaxAvailableBoostLevel());
    }

    public int ConsumeBoostLevel(int requestedBoostLevel)
    {
        int actualBoostLevel = ClampBoostLevel(requestedBoostLevel);
        SpendBP(actualBoostLevel);
        return actualBoostLevel;
    }

    // =========================
    // 升级
    // =========================
    public void LevelUp()
    {
        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
            return;
        }

        level++;
        maxHP += 20;        // 简单示例
        maxSP += 10;
        physicalAttack += 5;
        magicAttack += 5;
        physicalDefense += 3;
        magicDefense += 3;

        // 升级时全回复
        currentHP = EffectiveMaxHP;
        currentSP = EffectiveMaxSP;

        OnHPChanged?.Invoke(currentHP, EffectiveMaxHP);
        OnSPChanged?.Invoke(currentSP, EffectiveMaxSP);
        OnEXPChanged?.Invoke(currentExp, expToNextLevel);

        Debug.Log($"{unitName} 升级到 {level} 级！");

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
        }
    }

    public void AddExperience(int amount)
    {
        if (unitType != UnitType.Player || amount <= 0)
            return;

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
            return;
        }

        currentExp += amount;
        while (level < MaxLevel && currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
            if (level < MaxLevel)
                expToNextLevel = CalculateNextLevelExp(level);
        }

        if (level >= MaxLevel)
        {
            level = MaxLevel;
            currentExp = 0;
            expToNextLevel = 0;
        }

        OnEXPChanged?.Invoke(currentExp, expToNextLevel);
    }

    public void AddJP(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return;

        currentJP = Mathf.Max(0, currentJP + safeAmount);
        OnJPChanged?.Invoke(currentJP);
    }

    public bool TrySpendJP(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return true;

        if (currentJP < safeAmount)
            return false;

        currentJP -= safeAmount;
        OnJPChanged?.Invoke(currentJP);
        return true;
    }

    int CalculateNextLevelExp(int targetLevel)
    {
        return 100 + (targetLevel - 1) * 30;
    }
    
    // =========================
    // 治疗
    // =========================
    public void Heal(int amount)
    {
        currentHP += amount;
        currentHP = Mathf.Min(currentHP, EffectiveMaxHP);

        OnHPChanged?.Invoke(currentHP, EffectiveMaxHP);
    }

    // =========================
    // 攻击命中判定
    // =========================
    public bool CheckHit(BattleUnit target)
    {
        // Blind 状态：命中率 × 0.5
        float effectiveAccuracy = FinalAccuracy * (HasStatus(StatusEffectType.Blind) ? 0.5f : 1.0f);
        int hitChance = Mathf.RoundToInt(effectiveAccuracy) - target.evasion;
        hitChance = Mathf.Clamp(hitChance, 5, 100);
        return UnityEngine.Random.Range(0, 100) < hitChance;
    }

    // =========================
    // 是否暴击
    // =========================
    public bool CheckCrit()
    {
        return UnityEngine.Random.Range(0, 100) < FinalCritRate;
    }

    public void SetDefending(bool defending)
    {
        isDefending = defending;
    }

    public void BeginTurn()
    {
        if (isDefending)
        {
            isDefending = false;
            Debug.Log($"[Battle] {unitName} 的防御姿态结束");
        }
    }

    public bool IsImmuneTo(StatusEffectType type)
    {
        return statusImmunities.Contains(type);
    }

    public WeaponType GetResolvedNormalAttackWeaponType()
    {
        // 装备武器优先决定普攻击打类型
        if (equippedWeapon != null && equippedWeapon.weaponType != WeaponType.None)
            return equippedWeapon.weaponType;

        return normalAttackWeaponType != WeaponType.None
            ? normalAttackWeaponType
            : BattleFormula.ToWeaponType(normalAttackType);
    }

    public ElementType GetResolvedNormalAttackElementType()
    {
        return normalAttackElementType != ElementType.None
            ? normalAttackElementType
            : BattleFormula.ToElementType(normalAttackType);
    }

    public DamageType GetResolvedNormalAttackDamageType()
    {
        if (normalAttackDamageType == DamageType.Elemental)
            return DamageType.Elemental;

        ElementType resolvedElementType = GetResolvedNormalAttackElementType();
        WeaponType resolvedWeaponType = GetResolvedNormalAttackWeaponType();
        return (resolvedElementType != ElementType.None && resolvedWeaponType == WeaponType.None)
            ? DamageType.Elemental
            : DamageType.Physical;
    }

    public int GetWeaponAttack(WeaponType weaponType)
    {
        if (weaponType == WeaponType.None)
            return 0;

        // 已装备武器且类型匹配时，使用 WeaponData.pAtk
        if (equippedWeapon != null && equippedWeapon.weaponType == weaponType)
            return Mathf.Max(0, equippedWeapon.pAtk);

        // 回退到 Inspector 配置的 weaponAttackEntries（兼容旧数据）
        WeaponAttackEntry entry = weaponAttackEntries.Find(item => item != null && item.ResolveWeaponType() == weaponType);
        return entry != null ? Mathf.Max(0, entry.attackPower) : 0;
    }

    public int GetWeaponAttack(AttackType attackType)
    {
        return GetWeaponAttack(BattleFormula.ToWeaponType(attackType));
    }

    public int GetCombatAttackValue(DamageType damageType, WeaponType requiredWeaponType)
    {
        bool usesElementalFormula = damageType == DamageType.Elemental;
        int attributeValue = usesElementalFormula ? magicAttack : physicalAttack;
        float adjustedAttributeValue = attributeValue * AttackMultiplier;

        if (usesElementalFormula)
        {
            // 属性攻击：基础 E.Atk + 所有装备 E.Atk
            int equipEAtk = GetEquipmentTotalEAtk();
            return Mathf.Max(0, Mathf.RoundToInt(adjustedAttributeValue) + equipEAtk);
        }
        else
        {
            // 物理攻击：基础 P.Atk + 武器攻击（按武器类型） + 防具/饰品 P.Atk
            int weaponValue       = GetWeaponAttack(requiredWeaponType);
            int armorAccessoryAtk = GetEquipmentArmorAccessoryPAtk();
            return Mathf.Max(0, Mathf.RoundToInt(adjustedAttributeValue) + weaponValue + armorAccessoryAtk);
        }
    }

    public int GetCombatAttackValue(bool useMagicAttack, AttackType requiredWeaponType)
    {
        return GetCombatAttackValue(
            useMagicAttack ? DamageType.Elemental : DamageType.Physical,
            BattleFormula.ToWeaponType(requiredWeaponType));
    }

    public int GetCombatDefenseValue(DamageType damageType)
    {
        int baseValue  = damageType == DamageType.Elemental ? magicDefense   : physicalDefense;
        int equipBonus = damageType == DamageType.Elemental ? GetEquipmentTotalEDef() : GetEquipmentTotalPDef();
        return Mathf.Max(0, baseValue + equipBonus);
    }

    public int GetCombatDefenseValue(bool useMagicDefense)
    {
        return GetCombatDefenseValue(useMagicDefense ? DamageType.Elemental : DamageType.Physical);
    }

    // ============================================================
    // 装备系统
    // ============================================================

    /// <summary>
    /// 装备武器（自动更新属性缓存）。
    /// 传入 null 等效于 UnequipWeapon。
    /// </summary>
    public void EquipWeapon(WeaponData weapon)
    {
        if (!CanEquipWeapon(weapon))
        {
            string className = classDefinition != null ? classDefinition.GetDisplayNameOrFallback() : "无职业";
            string weaponType = weapon != null ? weapon.weaponType.ToString() : "None";
            Debug.LogWarning($"[Equip] {unitName}({className}) 无法装备武器类型: {weaponType}");
            return;
        }

        equippedWeapon = weapon;
        RecalculateEquipmentBonuses();
        Debug.Log($"[Equip] {unitName} 装备武器: {(weapon != null ? weapon.equipmentName : "无")}");
    }

    public bool CanEquipWeapon(WeaponData weapon)
    {
        if (weapon == null)
            return true;

        return CanEquipWeaponType(weapon.weaponType);
    }

    public bool CanEquipWeaponType(WeaponType weaponType)
    {
        if (classDefinition == null)
            return true;

        return classDefinition.CanEquipWeaponType(weaponType);
    }

    /// <summary>
    /// 装备防具（根据 ArmorData.slot 自动路由到 Head 或 Body 槽位）。
    /// 传入 null 会清空对应槽位（需配合 UnequipArmor）。
    /// </summary>
    public void EquipArmor(ArmorData armor)
    {
        if (armor == null) return;
        if (armor.slot == ArmorSlot.Head)
            equippedHead = armor;
        else
            equippedBody = armor;
        RecalculateEquipmentBonuses();
        Debug.Log($"[Equip] {unitName} 装备{armor.slot}: {armor.equipmentName}");
    }

    /// <summary>
    /// 装备饰品到指定槽位（0 或 1）。
    /// 传入 null 等效于 UnequipAccessory(slot)。
    /// </summary>
    public void EquipAccessory(AccessoryData accessory, int slot = 0)
    {
        int safeSlot = Mathf.Clamp(slot, 0, equippedAccessories.Length - 1);
        equippedAccessories[safeSlot] = accessory;
        RecalculateEquipmentBonuses();
        Debug.Log($"[Equip] {unitName} 装备饰品[{safeSlot}]: {(accessory != null ? accessory.equipmentName : "无")}");
    }

    /// <summary>卸下武器</summary>
    public void UnequipWeapon()
    {
        equippedWeapon = null;
        RecalculateEquipmentBonuses();
    }

    /// <summary>卸下指定槽位的防具</summary>
    public void UnequipArmor(ArmorSlot slot)
    {
        if (slot == ArmorSlot.Head) equippedHead = null;
        else                        equippedBody = null;
        RecalculateEquipmentBonuses();
    }

    /// <summary>卸下指定槽位的饰品</summary>
    public void UnequipAccessory(int slot)
    {
        int safeSlot = Mathf.Clamp(slot, 0, equippedAccessories.Length - 1);
        equippedAccessories[safeSlot] = null;
        RecalculateEquipmentBonuses();
    }

    /// <summary>
    /// 重新计算所有装备的 HP / SP 上限加成缓存。
    /// 装备发生变化时自动调用；战斗初始化（InitializeBattleState）也会触发。
    /// </summary>
    public void RecalculateEquipmentBonuses()
    {
        equipmentHPBonus = 0;
        equipmentSPBonus = 0;

        if (equippedWeapon != null) { equipmentHPBonus += equippedWeapon.hp; equipmentSPBonus += equippedWeapon.sp; }
        if (equippedHead   != null) { equipmentHPBonus += equippedHead.hp;   equipmentSPBonus += equippedHead.sp;   }
        if (equippedBody   != null) { equipmentHPBonus += equippedBody.hp;   equipmentSPBonus += equippedBody.sp;   }
        foreach (var acc in equippedAccessories)
        {
            if (acc == null) continue;
            equipmentHPBonus += acc.hp;
            equipmentSPBonus += acc.sp;
        }

        equipmentHPBonus = Mathf.Max(0, equipmentHPBonus);
        equipmentSPBonus = Mathf.Max(0, equipmentSPBonus);
    }

    // ── 装备属性聚合（内部使用） ──────────────────────────────────

    /// <summary>物理攻击：防具 + 饰品 pAtk 之和（武器 pAtk 由 GetWeaponAttack 按类型处理）</summary>
    private int GetEquipmentArmorAccessoryPAtk()
    {
        int bonus = 0;
        if (equippedHead != null) bonus += equippedHead.pAtk;
        if (equippedBody != null) bonus += equippedBody.pAtk;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.pAtk;
        return Mathf.Max(0, bonus);
    }

    /// <summary>属性攻击：全部装备 eAtk 之和</summary>
    private int GetEquipmentTotalEAtk()
    {
        int bonus = 0;
        if (equippedWeapon != null) bonus += equippedWeapon.eAtk;
        if (equippedHead   != null) bonus += equippedHead.eAtk;
        if (equippedBody   != null) bonus += equippedBody.eAtk;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.eAtk;
        return Mathf.Max(0, bonus);
    }

    /// <summary>物理防御：防具 + 饰品 pDef 之和</summary>
    private int GetEquipmentTotalPDef()
    {
        int bonus = 0;
        if (equippedHead != null) bonus += equippedHead.pDef;
        if (equippedBody != null) bonus += equippedBody.pDef;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.pDef;
        return Mathf.Max(0, bonus);
    }

    /// <summary>属性防御：防具 + 饰品 eDef 之和</summary>
    private int GetEquipmentTotalEDef()
    {
        int bonus = 0;
        if (equippedHead != null) bonus += equippedHead.eDef;
        if (equippedBody != null) bonus += equippedBody.eDef;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.eDef;
        return Mathf.Max(0, bonus);
    }

    /// <summary>速度：全部装备 speed 之和</summary>
    private int GetEquipmentTotalSpeed()
    {
        int bonus = 0;
        if (equippedWeapon != null) bonus += equippedWeapon.speed;
        if (equippedHead   != null) bonus += equippedHead.speed;
        if (equippedBody   != null) bonus += equippedBody.speed;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.speed;
        return bonus;
    }

    /// <summary>命中：全部装备 accuracy 之和</summary>
    private int GetEquipmentTotalAccuracy()
    {
        int bonus = 0;
        if (equippedWeapon != null) bonus += equippedWeapon.accuracy;
        if (equippedHead   != null) bonus += equippedHead.accuracy;
        if (equippedBody   != null) bonus += equippedBody.accuracy;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.accuracy;
        return bonus;
    }

    /// <summary>暴击：全部装备 crit 之和</summary>
    private int GetEquipmentTotalCrit()
    {
        int bonus = 0;
        if (equippedWeapon != null) bonus += equippedWeapon.crit;
        if (equippedHead   != null) bonus += equippedHead.crit;
        if (equippedBody   != null) bonus += equippedBody.crit;
        foreach (var acc in equippedAccessories)
            if (acc != null) bonus += acc.crit;
        return bonus;
    }

    /// <summary>
    /// 返回对指定异常状态的最终抗性比例（0~1）。
    /// 多件装备的同类抗性取最大值（而非叠加），避免完全免疫失控。
    /// FinalChance = BaseChance × (1 - resistance)
    /// </summary>
    public float GetEquipmentStatusResistance(StatusEffectType type)
    {
        float max = 0f;
        CheckResistance(equippedWeapon, type, ref max);
        CheckResistance(equippedHead,   type, ref max);
        CheckResistance(equippedBody,   type, ref max);
        foreach (var acc in equippedAccessories)
            CheckResistance(acc, type, ref max);
        return Mathf.Clamp01(max);
    }

    private static void CheckResistance(EquipmentData equip, StatusEffectType type, ref float max)
    {
        if (equip == null) return;
        float r = equip.GetResistance(type);
        if (r > max) max = r;
    }

    /// <summary>
    /// 判断装备后是否对指定状态实际免疫（抗性 = 1.0 视为完全免疫）。
    /// 优先于基础 statusImmunities 列表检查，两者任一满足即免疫。
    /// </summary>
    public bool IsImmuneByEquipment(StatusEffectType type)
    {
        return Mathf.Approximately(GetEquipmentStatusResistance(type), 1f);
    }

    /// <summary>
    /// 饰品开局 Buff 应用：战斗开始时由 BattleManager 调用。
    /// 遍历两枚饰品的所有 OpeningBuff 效果并施加到自身。
    /// </summary>
    public void ApplyAccessoryOpeningBuffs()
    {
        foreach (var acc in equippedAccessories)
        {
            if (acc == null) continue;
            foreach (var fx in acc.GetOpeningBuffEffects())
            {
                if (fx.buffType == StatusEffectType.None) continue;
                ApplyStatusEffect(new StatusEffect(fx.buffType, Mathf.Max(1, fx.buffDuration)));
                Debug.Log($"[Equip] {unitName} 饰品开局Buff: {fx.buffType} x{fx.buffDuration}回合");
            }
        }
    }

    /// <summary>
    /// 饰品开局 BP 加成：战斗开始时由 BattleManager 调用（在 ResetBP 之后）。
    /// </summary>
    public void ApplyAccessoryStartBPBonus()
    {
        foreach (var acc in equippedAccessories)
        {
            if (acc == null) continue;
            int bonus = Mathf.RoundToInt(acc.GetEffectValue(AccessoryEffectType.StartBPBonus));
            if (bonus > 0) GainBP(bonus);
        }
    }

    /// <summary>
    /// 获取饰品提供的 Break 期间伤害倍率额外加成（叠加于基础 ×2 之上）。
    /// </summary>
    public float GetAccessoryBreakDamageBonus()
    {
        float total = 0f;
        foreach (var acc in equippedAccessories)
            if (acc != null) total += acc.GetEffectValue(AccessoryEffectType.BreakDamageBonus);
        return total;
    }

    /// <summary>
    /// 获取饰品提供的全局伤害倍率加成（乘算，与基础倍率累乘）。
    /// </summary>
    public float GetAccessoryDamageMultiplierBonus()
    {
        float total = 0f;
        foreach (var acc in equippedAccessories)
            if (acc != null) total += acc.GetEffectValue(AccessoryEffectType.DamageMultiplierBonus);
        return total;
    }

    /// <summary>
    /// 获取饰品提供的命中弱点时额外护盾削减次数。
    /// </summary>
    public int GetAccessoryExtraShieldDamage()
    {
        int total = 0;
        foreach (var acc in equippedAccessories)
            if (acc != null) total += Mathf.RoundToInt(acc.GetEffectValue(AccessoryEffectType.ExtraShieldDamage));
        return total;
    }

    // =========================
    // 学习新技能
    // =========================
    public void LearnSkill(Skill newSkill, bool isArts = true)
    {
        if (newSkill == null)
            return;

        if (!CanLearnSkill(newSkill))
        {
            string className = classDefinition != null ? classDefinition.GetDisplayNameOrFallback() : "无职业";
            Debug.LogWarning($"[Skill] {unitName}({className}) 无法学习技能：{newSkill.skillName}");
            return;
        }

        if (isArts)
        {
            if (!artsList.Contains(newSkill))
            {
                artsList.Add(newSkill);
                Debug.Log($"{unitName} 学会了战技：{newSkill.skillName}");
            }
        }
        else
        {
            if (!skillList.Contains(newSkill))
            {
                skillList.Add(newSkill);
                Debug.Log($"{unitName} 学会了角色技能：{newSkill.skillName}");
            }
        }
    }

    public bool CanLearnSkill(Skill skill)
    {
        if (skill == null)
            return false;

        return skill.CanBeLearnedBy(this);
    }

    public void SanitizeSkillListsByClass()
    {
        RemoveInvalidSkillsFromList(artsList, "战技");
        RemoveInvalidSkillsFromList(skillList, "角色技能");
    }

    private void RemoveInvalidSkillsFromList(List<Skill> list, string listLabel)
    {
        if (list == null || list.Count == 0)
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Skill skill = list[i];
            if (skill == null)
                continue;

            if (CanLearnSkill(skill))
                continue;

            list.RemoveAt(i);
            string className = classDefinition != null ? classDefinition.GetDisplayNameOrFallback() : "无职业";
            Debug.LogWarning($"[Skill] {unitName}({className}) 的{listLabel}移除不匹配职业技能：{skill.skillName}");
        }
    }

    // =========================
    // 忘记技能
    // =========================
    public void ForgetSkill(Skill skillToRemove, bool isArts = true)
    {
        if (isArts)
        {
            if (artsList.Contains(skillToRemove))
            {
                artsList.Remove(skillToRemove);
                Debug.Log($"{unitName} 忘记了战技：{skillToRemove.skillName}");
            }
        }
        else
        {
            if (skillList.Contains(skillToRemove))
            {
                skillList.Remove(skillToRemove);
                Debug.Log($"{unitName} 忘记了角色技能：{skillToRemove.skillName}");
            }
        }
    }

    // =========================
    // 异常状态系统
    // =========================
    [Header("战斗状态")]
    public bool isBreak = false;                            // 破防状态
    public System.Collections.Generic.List<StatusEffect> activeEffects =
        new System.Collections.Generic.List<StatusEffect>();

    // ── 伤害倍率属性（供 BattleManager 查询）──
    /// <summary>受到伤害倍率：Freeze 时 ×1.5，Break 时 ×2.0</summary>
    public float IncomingDamageMultiplier
    {
        get
        {
            float multiplier = 1.0f;
            if (HasStatus(StatusEffectType.Freeze)) multiplier *= 1.5f;
            if (isBreak) multiplier *= 2.0f;
            return multiplier;
        }
    }
    /// <summary>攻击力倍率：Terror 时 ×0.7</summary>
    public float AttackMultiplier => HasStatus(StatusEffectType.Terror) ? 0.7f : 1.0f;

    // ── 行动能力属性 ──
    /// <summary>是否可以行动（Sleep / Freeze 会阻止行动）</summary>
    public bool CanAct    => !HasStatus(StatusEffectType.Sleep) && !HasStatus(StatusEffectType.Freeze);
    /// <summary>是否可以使用技能或战技</summary>
    public bool CanUseSkill => !HasStatus(StatusEffectType.Silence);
    /// <summary>是否处于混乱状态</summary>
    public bool IsConfused  => HasStatus(StatusEffectType.Confuse);
    /// <summary>是否处于震荡状态</summary>
    public bool IsShocked   => HasStatus(StatusEffectType.Shock);

    public bool HasStatus(StatusEffectType type)
    {
        return activeEffects.Exists(e => e.type == type);
    }

    /// <summary>施加异常状态；若已存在则取持续时间较长值（不叠层）</summary>
    public void ApplyStatusEffect(StatusEffect effect)
    {
        if (effect == null || effect.type == StatusEffectType.None)
            return;

        var existing = activeEffects.Find(e => e.type == effect.type);
        if (existing != null)
        {
            existing.remainingRounds = Mathf.Max(existing.remainingRounds, effect.remainingRounds);
            Debug.Log($"[Status] {unitName} 的 {effect.type} 持续时间刷新");
        }
        else
        {
            activeEffects.Add(effect);
            Debug.Log($"[Status] {unitName} 获得异常状态：{effect.type}");
        }
    }

    /// <summary>移除指定类型的异常状态</summary>
    public void RemoveStatusEffect(StatusEffectType type)
    {
        int count = activeEffects.RemoveAll(e => e.type == type);
        if (count > 0) Debug.Log($"[Status] {unitName} 解除了 {type} 状态");
    }

    /// <summary>清除所有异常状态与 Break 标记（战斗结束或复活时调用）</summary>
    public void ClearAllStatusEffects()
    {
        activeEffects.Clear();
        isBreak = false;
        breakSkipTurnCount = 0;
        isDefending = false;
        ResetShield();
    }

    /// <summary>每回合结束时调用：递减持续时间并移除到期状态</summary>
    public void TickStatusEffects()
    {
        activeEffects.RemoveAll(e =>
        {
            bool expired = e.Tick();
            if (expired) Debug.Log($"[Status] {unitName} 的 {e.type} 状态已解除");
            return expired;
        });
    }

    public bool IsWeakTo(AttackType attackType)
    {
        return weaknessTypes.Contains(attackType);
    }

    public bool IsWeakTo(WeaponType weaponType)
    {
        AttackType weaknessType = BattleFormula.ToAttackType(weaponType);
        return weaknessType != AttackType.None && IsWeakTo(weaknessType);
    }

    public bool IsWeakTo(ElementType elementType)
    {
        AttackType weaknessType = BattleFormula.ToAttackType(elementType);
        return weaknessType != AttackType.None && IsWeakTo(weaknessType);
    }

    public bool IsWeaknessRevealed(AttackType attackType)
    {
        return revealedWeaknessTypes.Contains(attackType);
    }

    public void ResetRevealedWeaknesses()
    {
        if (revealedWeaknessTypes.Count == 0)
            return;

        revealedWeaknessTypes.Clear();
        OnWeaknessStateChanged?.Invoke();
    }

    bool TryRevealWeakness(AttackType attackType)
    {
        if (attackType == AttackType.None || !IsWeakTo(attackType))
            return false;

        if (!revealedWeaknessTypes.Add(attackType))
            return false;

        Debug.Log($"[Weakness] {unitName} 的弱点 {attackType} 已被揭示");
        OnWeaknessRevealed?.Invoke(attackType);
        OnWeaknessStateChanged?.Invoke();
        return true;
    }

    public bool ApplyShieldDamage(AttackType attackType, int hitCount)
    {
        return ApplyShieldDamage(
            BattleFormula.ToWeaponType(attackType),
            BattleFormula.ToElementType(attackType),
            hitCount,
            false);
    }

    public bool ApplyShieldDamage(WeaponType weaponType, ElementType elementType, int hitCount, bool countBothWeaknessesSeparately = false)
    {
        if (unitType != UnitType.Enemy || maxShield <= 0)
            return false;

        bool weaponWeakHit = weaponType != WeaponType.None && IsWeakTo(weaponType);
        bool elementWeakHit = elementType != ElementType.None && IsWeakTo(elementType);

        if (weaponWeakHit)
            TryRevealWeakness(BattleFormula.ToAttackType(weaponType));

        if (elementWeakHit)
            TryRevealWeakness(BattleFormula.ToAttackType(elementType));

        int matchedWeaknessCount = (weaponWeakHit ? 1 : 0) + (elementWeakHit ? 1 : 0);
        if (matchedWeaknessCount <= 0 || isBreak)
            return false;

        int weaknessMultiplier = countBothWeaknessesSeparately ? Mathf.Max(1, matchedWeaknessCount) : 1;
        int shieldDamage = Mathf.Max(1, hitCount) * weaknessMultiplier;
        currentShield = Mathf.Max(0, currentShield - shieldDamage);
        OnShieldChanged?.Invoke(currentShield, maxShield);

        string weaknessLabel = weaponWeakHit && elementWeakHit
            ? $"{weaponType} + {elementType}"
            : weaponWeakHit ? weaponType.ToString() : elementType.ToString();
        Debug.Log($"[Shield] {unitName} 弱点命中 {weaknessLabel}，护盾 -{shieldDamage}，当前 {currentShield}/{maxShield}");

        return currentShield <= 0;
    }

    public void EnterBreak(int skipTurns)
    {
        if (unitType != UnitType.Enemy)
            return;

        isBreak = true;
        breakSkipTurnCount = Mathf.Max(breakSkipTurnCount, Mathf.Max(1, skipTurns));
        ApplyStatusEffect(new StatusEffect(StatusEffectType.Break, -1));
        OnShieldChanged?.Invoke(currentShield, maxShield);
        Debug.Log($"[Break] {unitName} 进入 Break，需跳过 {breakSkipTurnCount} 次行动");
    }

    public bool ConsumeBreakActionSkip()
    {
        if (!isBreak || breakSkipTurnCount <= 0)
            return false;

        breakSkipTurnCount--;
        Debug.Log($"[Break] {unitName} 被跳过一次行动，剩余跳过次数: {breakSkipTurnCount}");

        if (breakSkipTurnCount <= 0)
            ExitBreak();

        return true;
    }

    void ExitBreak()
    {
        isBreak = false;
        RemoveStatusEffect(StatusEffectType.Break);
        ResetShield();
        Debug.Log($"[Break] {unitName} Break 结束，护盾重置为 {currentShield}/{maxShield}");
    }

    public void ResetShield()
    {
        if (unitType == UnitType.Enemy && maxShield > 0)
            currentShield = maxShield;
        else
            currentShield = 0;

        OnShieldChanged?.Invoke(currentShield, maxShield);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 头像自动裁剪（从立绘生成 Portrait）
    // ─────────────────────────────────────────────────────────────────────────────

    private Sprite GeneratePortraitCrop(Sprite source)
    {
        if (source == null || source.texture == null)
            return source;

        Rect cropRect = BuildAdaptiveCropRect(source);
        return Sprite.Create(
            source.texture,
            cropRect,
            new Vector2(0.5f, 0.5f),
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            Vector4.zero,
            false);
    }

    private Rect BuildAdaptiveCropRect(Sprite source)
    {
        Rect src = source.rect;

        if (adaptiveCropByOpaqueBounds && TryGetOpaqueBounds(source, out Rect opaque))
        {
            if (TryBuildHeadPriorityCropRect(source, opaque, out Rect headPriorityCrop))
                return headPriorityCrop;

            float bodyW = Mathf.Max(1f, opaque.width);
            float bodyH = Mathf.Max(1f, opaque.height);
            float headH = bodyH * Mathf.Clamp(adaptiveHeadPortion, 0.2f, 0.8f);
            float size = Mathf.Max(
                bodyW * Mathf.Clamp(adaptiveWidthFactor, 0.5f, 1.6f),
                headH * Mathf.Clamp(adaptiveHeightFactor, 0.8f, 2.0f));
            float centerX = opaque.x + bodyW * 0.5f;
            float centerY = opaque.y + bodyH * (1f - adaptiveHeadPortion * 0.5f + adaptiveVerticalOffset);
            return ClampRectIntoSource(src, new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size));
        }

        float w = src.width * Mathf.Clamp(cropWidth, 0.05f, 1f);
        float h = src.height * Mathf.Clamp(cropHeight, 0.05f, 1f);
        float cx = src.x + src.width * Mathf.Clamp01(cropCenterX);
        float cy = src.y + src.height * Mathf.Clamp01(cropCenterY);
        return ClampRectIntoSource(src, new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h));
    }

    private bool TryBuildHeadPriorityCropRect(Sprite source, Rect bodyOpaque, out Rect result)
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
            return false;

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
        float topY = headOpaque.yMax + headH * Mathf.Clamp(headTopPadding, 0f, 0.6f);
        float centerY = topY - size * 0.5f;
        result = ClampRectIntoSource(source.rect, new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size));
        return true;
    }

    private Rect ClampRectIntoSource(Rect src, Rect r)
    {
        float w = Mathf.Min(src.width, Mathf.Max(1f, r.width));
        float h = Mathf.Min(src.height, Mathf.Max(1f, r.height));
        float x = Mathf.Clamp(r.x, src.x, src.xMax - w);
        float y = Mathf.Clamp(r.y, src.y, src.yMax - h);
        return new Rect(x, y, w, h);
    }

    private bool TryGetOpaqueBounds(Sprite source, out Rect bounds)
    {
        bounds = default;
        Texture2D tex = source.texture;
        if (tex == null || !tex.isReadable) return false;

        Rect src = source.rect;
        int x0 = Mathf.FloorToInt(src.x);
        int y0 = Mathf.FloorToInt(src.y);
        int w  = Mathf.FloorToInt(src.width);
        int h  = Mathf.FloorToInt(src.height);
        if (w <= 0 || h <= 0) return false;

        Color[] px;
        try { px = tex.GetPixels(x0, y0, w, h); }
        catch { return false; }

        int minX = w, minY = h, maxX = -1, maxY = -1;
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
        if (maxX < minX || maxY < minY) return false;
        bounds = new Rect(src.x + minX, src.y + minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private bool TryGetOpaqueBoundsInRect(Sprite source, Rect searchRect, out Rect bounds)
    {
        bounds = default;
        Rect src = source.rect;
        Rect clipped = Rect.MinMaxRect(
            Mathf.Max(src.xMin, searchRect.xMin),
            Mathf.Max(src.yMin, searchRect.yMin),
            Mathf.Min(src.xMax, searchRect.xMax),
            Mathf.Min(src.yMax, searchRect.yMax));
        if (clipped.width <= 0f || clipped.height <= 0f) return false;

        Texture2D tex = source.texture;
        if (tex == null || !tex.isReadable) return false;

        int x0 = Mathf.FloorToInt(clipped.x);
        int y0 = Mathf.FloorToInt(clipped.y);
        int w  = Mathf.FloorToInt(clipped.width);
        int h  = Mathf.FloorToInt(clipped.height);
        if (w <= 0 || h <= 0) return false;

        Color[] px;
        try { px = tex.GetPixels(x0, y0, w, h); }
        catch { return false; }

        int minX = w, minY = h, maxX = -1, maxY = -1;
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
        if (maxX < minX || maxY < minY) return false;
        bounds = new Rect(clipped.x + minX, clipped.y + minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }
}

public static class BattleFormula
{
    public const int DefaultMaxBP = 5;
    public const int MaxBoostLevel = 3;

    private const float MinLevelMultiplier = 0.58f;
    private const float MaxLevelMultiplier = 1.56f;
    private const int DamageCap = 9999;
    private const int HealCap = 9999;
    private const float NormalAttackBaseMultiplier = 1f;
    private const float DefaultDefenseMultiplier = 0.5f;
    private const float GuardDefenseMultiplier = 0.65f;
    private const float RandomMin = 0.98f;
    private const float RandomMax = 1.02f;
    private const float StatusStep = 1.5f;

    public static float GetLevelMultiplier(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 1, BattleUnit.MaxLevel);
        float t = (clampedLevel - 1f) / (BattleUnit.MaxLevel - 1f);
        return Mathf.Lerp(MinLevelMultiplier, MaxLevelMultiplier, t);
    }

    public static float GetBoostMultiplier(int boostLevel)
    {
        int clampedBoost = Mathf.Clamp(boostLevel, 0, MaxBoostLevel);
        return 1f + 0.9f * clampedBoost;
    }

    public static int GetBoostedAttackHitCount(int boostLevel)
    {
        return 1 + Mathf.Clamp(boostLevel, 0, MaxBoostLevel);
    }

    public static int GetBoostedTriggerChance(int baseChance, int boostLevel)
    {
        return Mathf.Clamp(Mathf.RoundToInt(baseChance * GetBoostMultiplier(boostLevel)), 0, 100);
    }

    public static int GetBoostedDuration(int baseRounds, int boostLevel, int roundsPerBoost = 1)
    {
        int extraRounds = Mathf.Max(0, boostLevel) * Mathf.Max(0, roundsPerBoost);
        return Mathf.Max(1, baseRounds + extraRounds);
    }

    public static bool IsWeaponAttackType(AttackType attackType)
    {
        return ToWeaponType(attackType) != WeaponType.None;
    }

    public static AttackType ToAttackType(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Sword: return AttackType.Sword;
            case WeaponType.Lance: return AttackType.Lance;
            case WeaponType.Dagger: return AttackType.Dagger;
            case WeaponType.Axe: return AttackType.Axe;
            case WeaponType.Bow: return AttackType.Bow;
            case WeaponType.Staff: return AttackType.Staff;
            default: return AttackType.None;
        }
    }

    public static AttackType ToAttackType(ElementType elementType)
    {
        switch (elementType)
        {
            case ElementType.Fire: return AttackType.Fire;
            case ElementType.Ice: return AttackType.Ice;
            case ElementType.Thunder: return AttackType.Thunder;
            case ElementType.Wind: return AttackType.Wind;
            case ElementType.Light: return AttackType.Light;
            case ElementType.Dark: return AttackType.Dark;
            default: return AttackType.None;
        }
    }

    public static WeaponType ToWeaponType(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Sword: return WeaponType.Sword;
            case AttackType.Lance: return WeaponType.Lance;
            case AttackType.Dagger: return WeaponType.Dagger;
            case AttackType.Axe: return WeaponType.Axe;
            case AttackType.Bow:
            case AttackType.Pierce: return WeaponType.Bow;
            case AttackType.Staff:
            case AttackType.Strike: return WeaponType.Staff;
            default: return WeaponType.None;
        }
    }

    public static ElementType ToElementType(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Fire: return ElementType.Fire;
            case AttackType.Ice: return ElementType.Ice;
            case AttackType.Thunder: return ElementType.Thunder;
            case AttackType.Wind: return ElementType.Wind;
            case AttackType.Light: return ElementType.Light;
            case AttackType.Dark: return ElementType.Dark;
            default: return ElementType.None;
        }
    }

    public static int CalculateNormalAttackDamage(BattleUnit attacker, BattleUnit target)
    {
        if (attacker == null || target == null)
            return 0;

        return CalculateDamageInternal(
            attacker,
            target,
            NormalAttackBaseMultiplier,
            attacker.GetResolvedNormalAttackDamageType(),
            0,
            attacker.GetResolvedNormalAttackWeaponType(),
            attacker.artsAbilityMultiplier,
            includeStatusModifier: true);
    }

    public static int CalculateArtDamage(BattleUnit user, BattleUnit target, float baseMultiplier, bool usesMagicFormula, int boostLevel, AttackType requiredWeaponType)
    {
        return CalculateArtDamage(
            user,
            target,
            baseMultiplier,
            usesMagicFormula ? DamageType.Elemental : DamageType.Physical,
            boostLevel,
            ToWeaponType(requiredWeaponType));
    }

    public static int CalculateArtDamage(BattleUnit user, BattleUnit target, float baseMultiplier, DamageType damageType, int boostLevel, WeaponType requiredWeaponType)
    {
        if (user == null || target == null)
            return 0;

        return CalculateDamageInternal(
            user,
            target,
            baseMultiplier,
            damageType,
            boostLevel,
            requiredWeaponType,
            user.artsAbilityMultiplier,
            includeStatusModifier: true);
    }

    public static int CalculateArtHeal(BattleUnit user, float baseMultiplier, int boostLevel)
    {
        if (user == null)
            return 0;

        float baseValue = -baseMultiplier * user.magicDefense * GetBoostMultiplier(boostLevel);
        float calculatedValue = baseValue * user.artsAbilityMultiplier * user.LevelMultiplier;
        float executedValue = calculatedValue * GetRandomVariance();
        int finalValue = Mathf.Max(Mathf.RoundToInt(executedValue), -HealCap);
        return Mathf.Abs(finalValue);
    }

    public static bool RollJudgement(int triggerChance)
    {
        int chance = Mathf.Clamp(triggerChance, 0, 100);
        return UnityEngine.Random.Range(0, 100) < chance;
    }

    public static float GetStatusModifier(BattleUnit attacker, BattleUnit target)
    {
        float modifier = 1f;

        if (attacker != null)
        {
            if (attacker.HasStatus(StatusEffectType.AttackUp))
                modifier *= StatusStep;

            if (attacker.HasStatus(StatusEffectType.AttackDown))
                modifier /= StatusStep;
        }

        if (target != null)
        {
            if (target.HasStatus(StatusEffectType.DefenseUp))
                modifier /= StatusStep;

            if (target.HasStatus(StatusEffectType.DefenseDown))
                modifier *= StatusStep;
        }

        return modifier;
    }

    private static int CalculateDamageInternal(
        BattleUnit attacker,
        BattleUnit target,
        float baseMultiplier,
        DamageType damageType,
        int boostLevel,
        WeaponType requiredWeaponType,
        float abilityMultiplier,
        bool includeStatusModifier)
    {
        float attackValue = attacker.GetCombatAttackValue(damageType, requiredWeaponType);
        float defenseValue = target.GetCombatDefenseValue(damageType);
        float defenseMultiplier = target.IsDefending ? GuardDefenseMultiplier : DefaultDefenseMultiplier;
        float baseDamage = ((baseMultiplier * attackValue) - (defenseMultiplier * defenseValue)) * GetBoostMultiplier(boostLevel);
        float statusModifier = includeStatusModifier ? GetStatusModifier(attacker, target) : 1f;
        float calculatedDamage = baseDamage * abilityMultiplier * statusModifier * attacker.LevelMultiplier;
        float executedDamage = calculatedDamage * GetRandomVariance() * Mathf.Max(0f, target.forcedDamageReductionMultiplier);
        float totalDamage = executedDamage * target.IncomingDamageMultiplier;
        int finalDamage = Mathf.Clamp(Mathf.RoundToInt(totalDamage), 0, DamageCap);
        return Mathf.Min(finalDamage, Mathf.Max(0, target.currentHP));
    }

    private static float GetRandomVariance()
    {
        return UnityEngine.Random.Range(RandomMin, RandomMax + 0.0001f);
    }
}