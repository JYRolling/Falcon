using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Attach to the Player root GameObject.
// Handles continuous fire pickup weapons, ammo depletion, and optional ammo UI.
//
// Metal Slug style:
//   - Pick up CrossbowPickup in world  -> Equip() is called with ammo count
//   - Hold Mouse0 while equipped       -> fires at fireRate shots/second
//   - Ammo hits 0                      -> weapon removed, Bow restored automatically
//   - Pick up same special weapon again -> ammo is added (up to maxAmmo)
public class CrossbowController : MonoBehaviour
{
    private enum WeaponSlot
    {
        Normal = 1,
        Pickup = 2
    }

    [Header("Ammo UI (optional)")]
    [Tooltip("Optional: icon image for the equipped crossbow. Auto-found if named 'CrossbowWeaponIcon'.")]
    [SerializeField] private Image weaponIconImage;

    [Tooltip("Optional: ammo number text. Auto-found if named 'CrossbowAmmoCountText'.")]
    [SerializeField] private TMP_Text ammoCountText;

    [Tooltip("Legacy fallback text. If this is assigned, it shows '<name>  <ammo>'. Auto-found if named 'CrossbowAmmoText'.")]
    [SerializeField] private TMP_Text ammoText;

    [Header("UI Highlight")]
    [Tooltip("Color used when pickup weapon slot (2) is active.")]
    [SerializeField] private Color activeUIColor = Color.white;

    [Tooltip("Color used when pickup weapon exists but slot 1 is currently active.")]
    [SerializeField] private Color inactiveUIColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Slot UI (optional)")]
    [Tooltip("Root RectTransform of slot 1 (normal bow).")]
    [SerializeField] private RectTransform normalSlotRoot;

    [Tooltip("Root RectTransform of slot 2 (pickup weapon).")]
    [SerializeField] private RectTransform pickupSlotRoot;

    [Tooltip("Optional border image for slot 1.")]
    [SerializeField] private Image normalSlotBorder;

    [Tooltip("Optional border image for slot 2.")]
    [SerializeField] private Image pickupSlotBorder;

    [SerializeField] private Color activeBorderColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color inactiveBorderColor = new Color(1f, 1f, 1f, 0.2f);

    [Header("Switch Animation")]
    [SerializeField] private bool usePopAnimation = true;
    [SerializeField] private float popScale = 1.12f;
    [SerializeField] private float popDuration = 0.12f;

    // Runtime state
    private CrossbowData currentData;
    private int currentAmmo;
    private float nextFireTime;
    private bool isEquipped;
    private WeaponSlot activeSlot = WeaponSlot.Normal;

    private Bow bowComponent;
    private AudioSource audioSource;
    private Coroutine normalSlotPopRoutine;
    private Coroutine pickupSlotPopRoutine;

    // Events – subscribe from UI or other systems
    public event Action<int, int> OnAmmoChanged;   // (current, max)
    public event Action<CrossbowData> OnEquipped;
    public event Action OnDepleted;

    public bool IsEquipped => isEquipped;
    public int CurrentAmmo => currentAmmo;
    public CrossbowData CurrentData => currentData;
    public bool IsPickupWeaponSelected => activeSlot == WeaponSlot.Pickup;

    private void Awake()
    {
        bowComponent = GetComponentInChildren<Bow>(true);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        if (weaponIconImage == null)
        {
            var iconGo = GameObject.Find("CrossbowWeaponIcon");
            if (iconGo != null) weaponIconImage = iconGo.GetComponent<Image>();
        }

        if (ammoCountText == null)
        {
            var countGo = GameObject.Find("CrossbowAmmoCountText");
            if (countGo != null) ammoCountText = countGo.GetComponent<TMP_Text>();
        }

        if (ammoText == null)
        {
            var go = GameObject.Find("CrossbowAmmoText");
            if (go != null) ammoText = go.GetComponent<TMP_Text>();
        }

        if (normalSlotRoot == null)
        {
            var go = GameObject.Find("WeaponSlot1Root");
            if (go != null) normalSlotRoot = go.GetComponent<RectTransform>();
        }

        if (pickupSlotRoot == null)
        {
            var go = GameObject.Find("WeaponSlot2Root");
            if (go != null) pickupSlotRoot = go.GetComponent<RectTransform>();
        }

        if (normalSlotBorder == null)
        {
            var go = GameObject.Find("WeaponSlot1Border");
            if (go != null) normalSlotBorder = go.GetComponent<Image>();
        }

        if (pickupSlotBorder == null)
        {
            var go = GameObject.Find("WeaponSlot2Border");
            if (go != null) pickupSlotBorder = go.GetComponent<Image>();
        }

        UpdateAmmoUI();
    }

    private void Update()
    {
        HandleWeaponSwitchInput();

        if (!isEquipped || activeSlot != WeaponSlot.Pickup)
            return;

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            Fire();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by CrossbowPickup when player walks into it.
    /// Equips the crossbow or stacks ammo if same data already active.
    /// </summary>
    public void Equip(CrossbowData data, int ammo)
    {
        if (data == null) return;

        // Same crossbow already equipped → just top up ammo
        if (isEquipped && currentData == data)
        {
            AddAmmo(ammo);
            return;
        }

        // Different or no crossbow active → equip fresh
        currentData = data;
        currentAmmo = Mathf.Clamp(ammo, 0, data.maxAmmo);
        isEquipped = true;
        nextFireTime = 0f;

        // Auto select pickup weapon when collected (Metal Slug style).
        SelectPickupWeapon();

        if (data.pickupSFX != null)
            audioSource.PlayOneShot(data.pickupSFX);

        UpdateAmmoUI();
        OnAmmoChanged?.Invoke(currentAmmo, data.maxAmmo);
        OnEquipped?.Invoke(data);
    }

    /// <summary>
    /// Add ammo to the currently equipped crossbow (capped at maxAmmo).
    /// </summary>
    public void AddAmmo(int amount)
    {
        if (!isEquipped || currentData == null) return;

        currentAmmo = Mathf.Min(currentAmmo + amount, currentData.maxAmmo);
        UpdateAmmoUI();
        OnAmmoChanged?.Invoke(currentAmmo, currentData.maxAmmo);
    }

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void Fire()
    {
        if (currentData == null || currentData.boltPrefab == null) return;

        nextFireTime = Time.time + 1f / Mathf.Max(0.01f, currentData.fireRate);

        // Position: use Bow's shotPoint (already aimed at mouse)
        Vector3 spawnPos = GetFirePosition();
        // Direction: use Bow's transform.right (Bow.Update rotates it toward mouse every frame)
        Vector2 aimDir = GetAimDirection();
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

        GameObject projectileGO = Instantiate(currentData.boltPrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));

        var bolt = projectileGO.GetComponent<CrossbowBolt>();
        if (bolt != null)
        {
            bolt.Init(currentData.damage, aimDir * currentData.bulletSpeed, currentData.groundLayer);
        }
        else
        {
            var arrow = projectileGO.GetComponent<Arrow>();
            if (arrow != null)
            {
                if (currentData.projectileArrowType != null)
                    arrow.SetType(currentData.projectileArrowType);
                else
                    arrow.WhatisGround(currentData.groundLayer);
            }

            // Fallback: drive Rigidbody2D directly if no CrossbowBolt component
            var projectileRb = projectileGO.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                if (arrow == null)
                    projectileRb.gravityScale = 0f;

                projectileRb.velocity = aimDir * currentData.bulletSpeed;
            }
        }

        // Muzzle flash VFX
        if (currentData.muzzleFlashPrefab != null)
            Instantiate(currentData.muzzleFlashPrefab, spawnPos, Quaternion.identity);

        // Fire SFX
        if (currentData.fireSFX != null)
            audioSource.PlayOneShot(currentData.fireSFX);

        currentAmmo--;
        UpdateAmmoUI();
        OnAmmoChanged?.Invoke(currentAmmo, currentData.maxAmmo);

        if (currentAmmo <= 0)
            Deplete();
    }

    private void Deplete()
    {
        SelectNormalWeapon();
        isEquipped = false;
        currentData = null;

        UpdateAmmoUI();
        OnDepleted?.Invoke();
    }

    private void HandleWeaponSwitchInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectNormalWeapon();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectPickupWeapon();
        }
    }

    private void SelectNormalWeapon()
    {
        activeSlot = WeaponSlot.Normal;

        if (bowComponent != null)
            bowComponent.ShootingOverridden = false;

        PlaySlotPop(normalSlotRoot, ref normalSlotPopRoutine);

        UpdateAmmoUI();
    }

    private bool SelectPickupWeapon()
    {
        if (!isEquipped || currentData == null || currentAmmo <= 0)
            return false;

        activeSlot = WeaponSlot.Pickup;

        if (bowComponent != null)
            bowComponent.ShootingOverridden = true;

        PlaySlotPop(pickupSlotRoot, ref pickupSlotPopRoutine);

        UpdateAmmoUI();
        return true;
    }

    private Vector3 GetFirePosition()
    {
        if (bowComponent != null && bowComponent.shotPoint != null)
            return bowComponent.shotPoint.position;
        return transform.position;
    }

    private Vector2 GetAimDirection()
    {
        // Bow.Update() sets transform.right = (mouse - bow), so just read it.
        if (bowComponent != null)
            return (Vector2)bowComponent.transform.right;

        // Fallback: manual mouse direction
        if (Camera.main != null)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 diff = mousePos - (Vector2)transform.position;
            if (diff.sqrMagnitude > 0.0001f)
                return diff.normalized;
        }
        return Vector2.right;
    }

    private void UpdateAmmoUI()
    {
        bool hasPickupWeapon = isEquipped && currentData != null;
        bool isPickupActive = activeSlot == WeaponSlot.Pickup;

        ApplySlotBorderHighlights(hasPickupWeapon, isPickupActive);

        if (hasPickupWeapon)
        {
            if (weaponIconImage != null)
            {
                weaponIconImage.sprite = currentData.icon;
                weaponIconImage.enabled = currentData.icon != null;
                ApplyImageHighlight(weaponIconImage, isPickupActive);
            }

            if (ammoCountText != null)
            {
                ammoCountText.text = currentAmmo.ToString();
                ammoCountText.enabled = true;
                ApplyTextHighlight(ammoCountText, isPickupActive);
            }

            // Legacy fallback text support
            if (ammoText != null)
            {
                ammoText.text = $"{currentData.displayName}  {currentAmmo}";
                ammoText.enabled = true;
                ApplyTextHighlight(ammoText, isPickupActive);
            }
        }
        else
        {
            if (weaponIconImage != null)
            {
                weaponIconImage.sprite = null;
                weaponIconImage.enabled = false;
            }

            if (ammoCountText != null)
            {
                ammoCountText.text = string.Empty;
                ammoCountText.enabled = false;
            }

            if (ammoText != null)
            {
                ammoText.text = string.Empty;
                ammoText.enabled = false;
            }
        }
    }

    private void ApplyImageHighlight(Image image, bool isActive)
    {
        if (image == null) return;
        image.color = isActive ? activeUIColor : inactiveUIColor;
    }

    private void ApplyTextHighlight(TMP_Text text, bool isActive)
    {
        if (text == null) return;
        text.color = isActive ? activeUIColor : inactiveUIColor;
    }

    private void ApplySlotBorderHighlights(bool hasPickupWeapon, bool isPickupActive)
    {
        bool isNormalActive = !isPickupActive;

        if (normalSlotBorder != null)
        {
            normalSlotBorder.enabled = true;
            normalSlotBorder.color = isNormalActive ? activeBorderColor : inactiveBorderColor;
        }

        if (pickupSlotBorder != null)
        {
            pickupSlotBorder.enabled = hasPickupWeapon;
            pickupSlotBorder.color = isPickupActive ? activeBorderColor : inactiveBorderColor;
        }
    }

    private void PlaySlotPop(RectTransform slotRoot, ref Coroutine routine)
    {
        if (!usePopAnimation || slotRoot == null)
            return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(PopSlotRoutine(slotRoot));
    }

    private IEnumerator PopSlotRoutine(RectTransform slotRoot)
    {
        Vector3 baseScale = Vector3.one;
        float duration = Mathf.Max(0.01f, popDuration);
        float halfDuration = duration * 0.5f;

        float t = 0f;
        while (t < halfDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / halfDuration);
            float scale = Mathf.Lerp(1f, popScale, p);
            slotRoot.localScale = baseScale * scale;
            yield return null;
        }

        t = 0f;
        while (t < halfDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / halfDuration);
            float scale = Mathf.Lerp(popScale, 1f, p);
            slotRoot.localScale = baseScale * scale;
            yield return null;
        }

        slotRoot.localScale = baseScale;
    }
}
