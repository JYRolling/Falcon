using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Attach to the Player root GameObject.
// Handles continuous fire crossbow pickup, ammo depletion, and optional ammo UI.
//
// Metal Slug style:
//   - Pick up CrossbowPickup in world  -> Equip() is called with ammo count
//   - Hold Mouse0 while equipped       -> fires at fireRate shots/second
//   - Ammo hits 0                      -> weapon removed, Bow restored automatically
//   - Pick up same crossbow again      -> ammo is added (up to maxAmmo)
public class CrossbowController : MonoBehaviour
{
    [Header("Ammo UI (optional)")]
    [Tooltip("Optional: icon image for the equipped crossbow. Auto-found if named 'CrossbowWeaponIcon'.")]
    [SerializeField] private Image weaponIconImage;

    [Tooltip("Optional: ammo number text. Auto-found if named 'CrossbowAmmoCountText'.")]
    [SerializeField] private TMP_Text ammoCountText;

    [Tooltip("Legacy fallback text. If this is assigned, it shows '<name>  <ammo>'. Auto-found if named 'CrossbowAmmoText'.")]
    [SerializeField] private TMP_Text ammoText;

    // Runtime state
    private CrossbowData currentData;
    private int currentAmmo;
    private float nextFireTime;
    private bool isEquipped;

    private Bow bowComponent;
    private AudioSource audioSource;

    // Events – subscribe from UI or other systems
    public event Action<int, int> OnAmmoChanged;   // (current, max)
    public event Action<CrossbowData> OnEquipped;
    public event Action OnDepleted;

    public bool IsEquipped => isEquipped;
    public int CurrentAmmo => currentAmmo;
    public CrossbowData CurrentData => currentData;

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

        UpdateAmmoUI();
    }

    private void Update()
    {
        if (!isEquipped) return;

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

        // Block Bow from firing while crossbow is active (Bow still rotates/aims)
        if (bowComponent != null)
            bowComponent.ShootingOverridden = true;

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

        GameObject boltGO = Instantiate(currentData.boltPrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));

        var bolt = boltGO.GetComponent<CrossbowBolt>();
        if (bolt != null)
        {
            bolt.Init(currentData.damage, aimDir * currentData.bulletSpeed, currentData.groundLayer);
        }
        else
        {
            // Fallback: drive Rigidbody2D directly if no CrossbowBolt component
            var boltRb = boltGO.GetComponent<Rigidbody2D>();
            if (boltRb != null)
            {
                boltRb.gravityScale = 0f;
                boltRb.velocity = aimDir * currentData.bulletSpeed;
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
        isEquipped = false;
        currentData = null;

        // Give shooting back to the Bow
        if (bowComponent != null)
            bowComponent.ShootingOverridden = false;

        UpdateAmmoUI();
        OnDepleted?.Invoke();
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
        if (isEquipped && currentData != null)
        {
            if (weaponIconImage != null)
            {
                weaponIconImage.sprite = currentData.icon;
                weaponIconImage.enabled = currentData.icon != null;
            }

            if (ammoCountText != null)
            {
                ammoCountText.text = currentAmmo.ToString();
                ammoCountText.enabled = true;
            }

            // Legacy fallback text support
            if (ammoText != null)
            {
                ammoText.text = $"{currentData.displayName}  {currentAmmo}";
                ammoText.enabled = true;
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
}
