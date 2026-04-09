using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WeaknessSlotUI : MonoBehaviour
{
    [Header("节点引用")]
    public Image iconImage;
    public GameObject unknownMask;
    public GameObject lockOverlay;
    public Graphic hitFlashGraphic;

    [Header("揭示闪光")]
    [Range(0.05f, 1f)] public float flashDuration = 0.20f;
    public Color flashColor = new Color(1f, 1f, 1f, 0.85f);

    private AttackType currentType = AttackType.None;
    private bool hasState = false;
    private bool isRevealed = false;

    void Awake()
    {
        AutoAssignReferences();
        SetFlashVisible(false, 0f);
    }

    public void SetState(AttackType attackType, Sprite iconSprite, bool revealed)
    {
        AutoAssignReferences();

        bool hasVisibleIcon = revealed && iconSprite != null;
        bool justRevealed = hasState && !isRevealed && revealed && currentType == attackType;

        currentType = attackType;
        isRevealed = revealed;
        hasState = true;

        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = hasVisibleIcon;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
        }

        SafeSetActive(unknownMask, !revealed || !hasVisibleIcon);
        SafeSetActive(lockOverlay, !revealed);

        if (justRevealed)
            PlayRevealFlash();
        else
            SetFlashVisible(false, 0f);
    }

    public void ClearState()
    {
        currentType = AttackType.None;
        isRevealed = false;
        hasState = false;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        SafeSetActive(unknownMask, false);
        SafeSetActive(lockOverlay, false);
        SetFlashVisible(false, 0f);
    }

    public void PlayRevealFlash()
    {
        if (hitFlashGraphic == null)
            return;

        StopAllCoroutines();
        StartCoroutine(PlayRevealFlashRoutine());
    }

    IEnumerator PlayRevealFlashRoutine()
    {
        const int steps = 4;
        float halfDuration = flashDuration * 0.5f;

        for (int i = 0; i < steps; i++)
        {
            float alpha = Mathf.Lerp(0f, flashColor.a, (i + 1) / (float)steps);
            SetFlashVisible(true, alpha);
            yield return new WaitForSeconds(halfDuration / steps);
        }

        for (int i = 0; i < steps; i++)
        {
            float alpha = Mathf.Lerp(flashColor.a, 0f, (i + 1) / (float)steps);
            SetFlashVisible(true, alpha);
            yield return new WaitForSeconds(halfDuration / steps);
        }

        SetFlashVisible(false, 0f);
    }

    void SetFlashVisible(bool visible, float alpha)
    {
        if (hitFlashGraphic == null)
            return;

        hitFlashGraphic.gameObject.SetActive(visible);
        Color color = flashColor;
        color.a = alpha;
        hitFlashGraphic.color = color;
    }

    void AutoAssignReferences()
    {
        if (iconImage == null)
            iconImage = transform.Find("Image")?.GetComponent<Image>();

        if (unknownMask == null)
        {
            Transform node = transform.Find("UnknownMask");
            if (node != null) unknownMask = node.gameObject;
        }

        if (lockOverlay == null)
        {
            Transform node = transform.Find("LockOverlay");
            if (node != null) lockOverlay = node.gameObject;
        }

        if (hitFlashGraphic == null)
            hitFlashGraphic = transform.Find("HitFlash")?.GetComponent<Graphic>();
    }

    static void SafeSetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }
}
