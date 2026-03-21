using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Bow : MonoBehaviour
{
    // legacy single-arrow fallback
    private GameObject arrow;

    // arrow prefabs (for cycling)
    public GameObject[] arrowPrefabs;
    int selectedArrowIndex = 0;

    // Optional: directly assign ArrowType ScriptableObjects for icons/data
    private ArrowType[] arrowTypes;

    // UI: assign an Image on your Canvas to display the current arrow icon (legacy)
    [Header("UI")]
    public Image shootTypeIcon;         // arrow icon (existing)
    public Image shootingStyleIcon;     // shooting type icon (new)

    // New: TextMeshPro fields to display names
    public TMP_Text shootingStyleNameText;
    public TMP_Text arrowTypeNameText;

    public float launchForce;
    public Transform shotPoint;

    // Shooting type ScriptableObjects (new)
    [Header("Shooting Types (ScriptableObjects)")]
    public ShootingType[] shootingTypes;
    int selectedShootingTypeIndex = 0;

    // Default shooting type used when none are assigned (assign a ShootingType asset in Inspector)
    [Tooltip("Optional default ShootingType to use when no shootingTypes are configured.")]
    private ShootingType defaultShootingType;

    // Backwards-compatible enum fallback
    private ShootingStyle fallbackShootingStyle = ShootingStyle.Single;

    // Multi-shot transforms (optional)
    [Header("Multi Shot Points (assign Transforms or leave empty to use shotPoint)")]
    private Transform multiShotPoint1;
    private Transform multiShotPoint2;

    public GameObject point;
    GameObject[] points;
    public int numberOfPoints;
    public float spaceBetweenPoints;
    Vector2 direction;

    [Header("Aim")]
    [Tooltip("Clamp bow aim angle relative to parent rotation.")]
    public bool clampAimAngle = false;
    [Tooltip("Minimum local aim angle in degrees.")]
    public float minAimAngle = -70f;
    [Tooltip("Maximum local aim angle in degrees.")]
    public float maxAimAngle = 70f;

    [Header("Anti-spam")]
    [Tooltip("Seconds between allowed shots. Increase to prevent spam clicking.")]
    public float shootCooldown = 0.25f;
    private float _lastShootTime = -999f;

    // Set true by CrossbowController (or any weapon override) to block bow from shooting.
    [HideInInspector] public bool ShootingOverridden = false;

    AudioManager audioManager;

    private void OnEnable()
    {
        // Resolve UI when the object becomes active (covers instantiation/respawn)
        ResolveUIReferences();
        UpdateShootTypeIcon();
        UpdateShootingTypeIcon();
        UpdateNameTexts();
    }

    private void Awake()
    {
        audioManager = GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioManager>();
    }

    private void Start()
    {
        points = new GameObject[numberOfPoints];
        for (int i = 0; i < numberOfPoints; i++)
        {
            points[i] = Instantiate(point, shotPoint.position, Quaternion.identity);
        }

        // Ensure UI is wired (Start may run after OnEnable; safe to call again)
        ResolveUIReferences();
        UpdateShootTypeIcon();
        UpdateShootingTypeIcon();
        UpdateNameTexts();
    }

    // Try to find scene UI elements if they aren't assigned in the Inspector.
    // Name-based lookup: use these GameObject names or assign references manually in Inspector.
    void ResolveUIReferences()
    {
        if (shootTypeIcon == null)
        {
            var go = GameObject.Find("ShootTypeIcon");
            if (go != null) shootTypeIcon = go.GetComponent<Image>();
        }

        if (shootingStyleIcon == null)
        {
            var go = GameObject.Find("ShootingStyleIcon");
            if (go != null) shootingStyleIcon = go.GetComponent<Image>();
        }

        if (shootingStyleNameText == null)
        {
            var go = GameObject.Find("ShootingStyleNameText");
            if (go != null) shootingStyleNameText = go.GetComponent<TMP_Text>();
        }

        if (arrowTypeNameText == null)
        {
            var go = GameObject.Find("ArrowTypeNameText");
            if (go != null) arrowTypeNameText = go.GetComponent<TMP_Text>();
        }

        // If still missing, try looser find (first matching component in scene).
        if (shootTypeIcon == null)
        {
            var found = FindObjectOfType<Image>();
            if (found != null) shootTypeIcon = found;
        }
        if (shootingStyleIcon == null)
        {
            var images = FindObjectsOfType<Image>();
            foreach (var img in images)
            {
                if (img.name.ToLower().Contains("shooting") || img.name.ToLower().Contains("style"))
                {
                    shootingStyleIcon = img;
                    break;
                }
            }
        }
        if (shootingStyleNameText == null)
        {
            var t = FindObjectOfType<TMP_Text>();
            if (t != null) shootingStyleNameText = t;
        }
        if (arrowTypeNameText == null)
        {
            var texts = FindObjectsOfType<TMP_Text>();
            foreach (var tt in texts)
            {
                if (tt.name.ToLower().Contains("arrow") || tt.name.ToLower().Contains("type"))
                {
                    arrowTypeNameText = tt;
                    break;
                }
            }
        }

        // Final diagnostic logs
        if (shootTypeIcon == null) Debug.LogWarning("[Bow] shootTypeIcon not assigned and was not found in scene.");
        if (shootingStyleIcon == null) Debug.LogWarning("[Bow] shootingStyleIcon not assigned and was not found in scene.");
        if (shootingStyleNameText == null) Debug.LogWarning("[Bow] shootingStyleNameText not assigned and was not found in scene.");
        if (arrowTypeNameText == null) Debug.LogWarning("[Bow] arrowTypeNameText not assigned and was not found in scene.");
    }

    void Update()
    {
        UpdateAimDirection();

        // Enforce cooldown to prevent spam clicking; skip when another weapon has priority
        if (Input.GetMouseButtonDown(0) && !ShootingOverridden)
        {
            if (Time.time >= _lastShootTime + shootCooldown)
            {
                Shoot();
                _lastShootTime = Time.time;
            }
            else
            {
                // Optionally: provide feedback (sound/UI) for cooldown here
            }
        }

        // Press F to cycle arrow types (legacy)
        if (Input.GetKeyDown(KeyCode.F))
        {
            CycleArrow();
        }

        // Press R to cycle shooting types (ScriptableObjects) or toggle fallback
        if (Input.GetKeyDown(KeyCode.R))
        {
            CycleOrToggleShootingType();
        }

        for (int i = 0; i < numberOfPoints; i++)
        {
            points[i].transform.position = PointPosition(i * spaceBetweenPoints);
        }
    }

    void UpdateAimDirection()
    {
        if (Camera.main == null) return;

        Vector2 bowPosition = transform.position;
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        direction = mousePosition - bowPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float worldAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (clampAimAngle)
        {
            Vector2 baseDirection = transform.parent != null ? (Vector2)transform.parent.right : Vector2.right;
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            float localAngle = Mathf.DeltaAngle(baseAngle, worldAngle);
            localAngle = Mathf.Clamp(localAngle, minAimAngle, maxAimAngle);
            worldAngle = baseAngle + localAngle;
        }

        // Force left-facing pose to use Y=180 (instead of Euler showing X=-180).
        Vector2 facingDir = transform.parent != null ? (Vector2)transform.parent.right : Vector2.right;
        bool facingLeft = facingDir.x < 0f;

        if (facingLeft)
        {
            transform.rotation = Quaternion.Euler(0f, 180f, 180f - worldAngle);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, worldAngle);
        }

        float rad = worldAngle * Mathf.Deg2Rad;
        direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    void Shoot()
    {
        audioManager.PlaySFX(audioManager.shoot3);
        // prefer shooting-type provided arrow prefab; otherwise use selected arrow prefab/fallback arrow
        ShootingType currentType = GetCurrentShootingType();
        GameObject prefab = currentType != null && currentType.arrowPrefab != null ? currentType.arrowPrefab : GetSelectedArrowPrefab();
        if (prefab == null) return;

        float effectiveLaunch = launchForce * (currentType != null ? currentType.launchForceMultiplier : 1f);
        ShootingStyle style = currentType != null ? currentType.shootingStyle : fallbackShootingStyle;

        // If style is Single OR currentType indicates single projectile, spawn a single arrow.
        // If style is Multi but currentType is null, use defaultShootingType if provided, otherwise safe defaults.
        if (style == ShootingStyle.Single || (currentType != null && currentType.projectileCount <= 1))
        {
            SpawnArrowAtTransform(prefab, shotPoint, 0f, effectiveLaunch, currentType);
            return;
        }

        // Multi-shot branch (safe even when currentType is null)
        int count = (currentType != null) ? Mathf.Max(1, currentType.projectileCount) : 1;
        float totalSpread = (currentType != null) ? currentType.spreadAngle : 0f;

        // If currentType is null but fallback is Multi, handle cleanly:
        if (currentType == null && style == ShootingStyle.Multi)
        {
            if (defaultShootingType != null)
            {
                // use defaultShootingType instead of warning-only
                currentType = defaultShootingType;
                count = Mathf.Max(1, currentType.projectileCount);
                totalSpread = currentType.spreadAngle;
                effectiveLaunch = launchForce * currentType.launchForceMultiplier;
                Debug.Log("[Bow] Using defaultShootingType for multi-shot fallback: " + currentType.displayName);
            }
            else
            {
                Debug.LogWarning("[Bow] fallbackShootingStyle is Multi but no ShootingType is assigned � using single-shot fallback values.");
            }
        }

        float step = (count > 1) ? totalSpread / (count - 1) : 0f;
        float startAngle = -totalSpread / 2f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * step;
            SpawnArrowAtTransform(prefab, shotPoint, angle, effectiveLaunch, currentType);
        }

        // optional: also spawn from additional shot points if assigned (keeps existing behavior)
        if (multiShotPoint1 != null)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * step;
                SpawnArrowAtTransform(prefab, multiShotPoint1, angle, effectiveLaunch, currentType);
            }
        }
        if (multiShotPoint2 != null)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * step;
                SpawnArrowAtTransform(prefab, multiShotPoint2, angle, effectiveLaunch, currentType);
            }
        }
    }

    void SpawnArrowAtTransform(GameObject prefab, Transform spawnTransform, float angleOffsetDeg, float effectiveLaunch, ShootingType type)
    {
        if (spawnTransform == null) return;
        Quaternion rot = spawnTransform.rotation * Quaternion.Euler(0, 0, angleOffsetDeg);
        GameObject newArrow = Instantiate(prefab, spawnTransform.position, rot);

        // Apply ArrowType data if ShootingType provides it (keeps Arrow.SetType usage)
        var arrowComponent = newArrow.GetComponent<Arrow>();
        if (arrowComponent != null && type != null && type.arrowType != null)
        {
            arrowComponent.SetType(type.arrowType);
        }

        Rigidbody2D rb = newArrow.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 launchDir = rot * Vector3.right;
            rb.velocity = launchDir * effectiveLaunch;
        }
    }

    GameObject GetSelectedArrowPrefab()
    {
        if (arrowPrefabs != null && arrowPrefabs.Length > 0)
        {
            selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, 0, arrowPrefabs.Length - 1);
            return arrowPrefabs[selectedArrowIndex];
        }
        return arrow;
    }

    void CycleArrow()
    {
        if ((arrowPrefabs != null && arrowPrefabs.Length > 0) || (arrowTypes != null && arrowTypes.Length > 0))
        {
            int length = (arrowTypes != null && arrowTypes.Length > 0) ? arrowTypes.Length : arrowPrefabs.Length;
            selectedArrowIndex = (selectedArrowIndex + 1) % length;
            Debug.Log($"Selected arrow [{selectedArrowIndex}]");
            UpdateShootTypeIcon();
            UpdateNameTexts();
        }
        else
        {
            Debug.Log("No arrow prefabs/arrow types assigned. Assign prefabs or use the single 'arrow' field.");
        }
    }

    void CycleOrToggleShootingType()
    {
        if (shootingTypes != null && shootingTypes.Length > 0)
        {
            selectedShootingTypeIndex = (selectedShootingTypeIndex + 1) % shootingTypes.Length;
            Debug.Log($"Selected shooting type [{selectedShootingTypeIndex}]: {shootingTypes[selectedShootingTypeIndex].displayName}");
            UpdateShootingTypeIcon();
            // also update arrow icon because the selected ShootingType may define a different arrow/arrowType
            UpdateShootTypeIcon();
            UpdateNameTexts();
        }
        else
        {
            // fallback behavior: toggle boolean-style enum
            fallbackShootingStyle = (fallbackShootingStyle == ShootingStyle.Single) ? ShootingStyle.Multi : ShootingStyle.Single;
            Debug.Log($"Shooting style (fallback): {fallbackShootingStyle}");
            UpdateShootingTypeIcon();
            UpdateShootTypeIcon();
            UpdateNameTexts();
        }
    }

    Vector2 PointPosition(float t)
    {
        Vector2 position = (Vector2)shotPoint.position
                           + (direction.normalized * launchForce * t)
                           + (0.5f * Physics2D.gravity * (t * t));
        return position;
    }

    // ---- ICON SUPPORT ----
    void UpdateShootTypeIcon()
    {
        if (shootTypeIcon == null) return;
        Sprite s = GetSelectedArrowSprite();
        if (s != null)
        {
            shootTypeIcon.sprite = s;
            shootTypeIcon.enabled = true;
            shootTypeIcon.color = new Color(shootTypeIcon.color.r, shootTypeIcon.color.g, shootTypeIcon.color.b, 1f);
        }
        else
        {
            shootTypeIcon.sprite = null;
            shootTypeIcon.enabled = false;
        }
    }

    Sprite GetSelectedArrowSprite()
    {
        // 0) Prefer current ShootingType's arrow/icon (if any)
        var currentSType = GetCurrentShootingType();
        if (currentSType != null)
        {
            // prefer icon on the ArrowType (UI)
            if (currentSType.arrowType != null && currentSType.arrowType.icon != null)
                return currentSType.arrowType.icon;

            // then prefer sprite on the shooting-type's arrowPrefab (use sprite as fallback for UI)
            if (currentSType.arrowPrefab != null)
            {
                var arrowCompPrefab = currentSType.arrowPrefab.GetComponent<Arrow>();
                if (arrowCompPrefab != null && arrowCompPrefab.arrowType != null)
                {
                    if (arrowCompPrefab.arrowType.icon != null) return arrowCompPrefab.arrowType.icon;
                    if (arrowCompPrefab.arrowType.sprite != null) return arrowCompPrefab.arrowType.sprite;
                }

                var srPrefab = currentSType.arrowPrefab.GetComponentInChildren<SpriteRenderer>();
                if (srPrefab != null && srPrefab.sprite != null)
                    return srPrefab.sprite;
            }
        }

        // 1) Prefer ArrowType[] assigned in the inspector (legacy) � use icon first
        if (arrowTypes != null && arrowTypes.Length > 0)
        {
            selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, 0, arrowTypes.Length - 1);
            if (arrowTypes[selectedArrowIndex] != null)
            {
                if (arrowTypes[selectedArrowIndex].icon != null) return arrowTypes[selectedArrowIndex].icon;
                if (arrowTypes[selectedArrowIndex].sprite != null) return arrowTypes[selectedArrowIndex].sprite;
            }
        }

        // 2) Try to read ArrowType from the prefab's Arrow component
        if (arrowPrefabs != null && arrowPrefabs.Length > 0)
        {
            selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, 0, arrowPrefabs.Length - 1);
            var prefab = arrowPrefabs[selectedArrowIndex];
            if (prefab != null)
            {
                var arrowComp = prefab.GetComponent<Arrow>();
                if (arrowComp != null && arrowComp.arrowType != null)
                {
                    if (arrowComp.arrowType.icon != null) return arrowComp.arrowType.icon;
                    if (arrowComp.arrowType.sprite != null) return arrowComp.arrowType.sprite;
                }

                // fallback: sprite from a child SpriteRenderer on prefab
                var sr = prefab.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    return sr.sprite;
            }
        }

        // 3) Legacy single arrow fallback
        if (arrow != null)
        {
            var arrowComp = arrow.GetComponent<Arrow>();
            if (arrowComp != null && arrowComp.arrowType != null)
            {
                if (arrowComp.arrowType.icon != null) return arrowComp.arrowType.icon;
                if (arrowComp.arrowType.sprite != null) return arrowComp.arrowType.sprite;
            }
            var sr = arrow.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                return sr.sprite;
        }

        return null;
    }

    // ---- ShootingType Icon ----
    void UpdateShootingTypeIcon()
    {
        if (shootingStyleIcon == null) return;

        ShootingType current = GetCurrentShootingType();
        if (current != null && current.icon != null)
        {
            shootingStyleIcon.sprite = current.icon;
            shootingStyleIcon.enabled = true;
            shootingStyleIcon.color = new Color(shootingStyleIcon.color.r, shootingStyleIcon.color.g, shootingStyleIcon.color.b, 1f);
        }
        else
        {
            // fallback: show simple text/icon disabled or based on fallback enum (optional)
            shootingStyleIcon.sprite = null;
            shootingStyleIcon.enabled = false;
        }
    }

    // ---- NAME TEXT SUPPORT ----
    void UpdateNameTexts()
    {
        UpdateShootingStyleNameText();
        UpdateArrowTypeNameText();
    }

    void UpdateShootingStyleNameText()
    {
        if (shootingStyleNameText == null) return;
        var current = GetCurrentShootingType();
        string name;
        if (current != null && !string.IsNullOrEmpty(current.displayName))
            name = current.displayName;
        else
            name = fallbackShootingStyle.ToString();

        shootingStyleNameText.text = name ?? "";
        shootingStyleNameText.enabled = !string.IsNullOrEmpty(name);
    }

    void UpdateArrowTypeNameText()
    {
        if (arrowTypeNameText == null) return;
        string name = GetSelectedArrowName();
        arrowTypeNameText.text = name ?? "";
        arrowTypeNameText.enabled = !string.IsNullOrEmpty(name);
    }

    string GetSelectedArrowName()
    {
        // 0) Prefer current ShootingType's arrowType.displayName
        var currentSType = GetCurrentShootingType();
        if (currentSType != null)
        {
            if (currentSType.arrowType != null && !string.IsNullOrEmpty(currentSType.arrowType.displayName))
                return currentSType.arrowType.displayName;

            if (currentSType.arrowPrefab != null)
            {
                var arrowCompPrefab = currentSType.arrowPrefab.GetComponent<Arrow>();
                if (arrowCompPrefab != null && arrowCompPrefab.arrowType != null && !string.IsNullOrEmpty(arrowCompPrefab.arrowType.displayName))
                    return arrowCompPrefab.arrowType.displayName;

                // use prefab name as fallback
                return currentSType.arrowPrefab.name;
            }
        }

        // 1) ArrowType[] assigned in inspector (legacy)
        if (arrowTypes != null && arrowTypes.Length > 0)
        {
            selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, 0, arrowTypes.Length - 1);
            if (arrowTypes[selectedArrowIndex] != null && !string.IsNullOrEmpty(arrowTypes[selectedArrowIndex].displayName))
                return arrowTypes[selectedArrowIndex].displayName;
        }

        // 2) Arrow prefab's Arrow component
        if (arrowPrefabs != null && arrowPrefabs.Length > 0)
        {
            selectedArrowIndex = Mathf.Clamp(selectedArrowIndex, 0, arrowPrefabs.Length - 1);
            var prefab = arrowPrefabs[selectedArrowIndex];
            if (prefab != null)
            {
                var arrowComp = prefab.GetComponent<Arrow>();
                if (arrowComp != null && arrowComp.arrowType != null && !string.IsNullOrEmpty(arrowComp.arrowType.displayName))
                    return arrowComp.arrowType.displayName;

                return prefab.name;
            }
        }

        // 3) Legacy single arrow fallback
        if (arrow != null)
        {
            var arrowComp = arrow.GetComponent<Arrow>();
            if (arrowComp != null && arrowComp.arrowType != null && !string.IsNullOrEmpty(arrowComp.arrowType.displayName))
                return arrowComp.arrowType.displayName;
            return arrow.name;
        }

        return null;
    }

    ShootingType GetCurrentShootingType()
    {
        if (shootingTypes != null && shootingTypes.Length > 0)
        {
            selectedShootingTypeIndex = Mathf.Clamp(selectedShootingTypeIndex, 0, shootingTypes.Length - 1);
            return shootingTypes[selectedShootingTypeIndex];
        }

        // use default if provided
        if (defaultShootingType != null)
            return defaultShootingType;

        return null;
    }

    public bool UnlockShootingType(ShootingType newType, bool equipImmediately)
    {
        if (newType == null)
            return false;

        int existingIndex = FindShootingTypeIndex(newType);
        if (existingIndex >= 0)
        {
            if (equipImmediately)
                selectedShootingTypeIndex = existingIndex;

            RefreshTypeUI();
            return false;
        }

        int oldLen = shootingTypes != null ? shootingTypes.Length : 0;
        ShootingType[] next = new ShootingType[oldLen + 1];

        for (int i = 0; i < oldLen; i++)
            next[i] = shootingTypes[i];

        next[oldLen] = newType;
        shootingTypes = next;

        if (equipImmediately)
            selectedShootingTypeIndex = oldLen;

        RefreshTypeUI();
        return true;
    }

    private int FindShootingTypeIndex(ShootingType type)
    {
        if (type == null || shootingTypes == null)
            return -1;

        for (int i = 0; i < shootingTypes.Length; i++)
        {
            if (shootingTypes[i] == type)
                return i;
        }

        return -1;
    }

    private void RefreshTypeUI()
    {
        UpdateShootingTypeIcon();
        UpdateShootTypeIcon();
        UpdateNameTexts();
    }
}
