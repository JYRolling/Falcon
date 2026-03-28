using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Runtime component added to enemies by frost arrows.
// It slows common movement speed fields for a short duration and then restores them.
public class EnemyFrostDebuff : MonoBehaviour
{
    private sealed class ModifiedSpeedField
    {
        public MonoBehaviour component;
        public FieldInfo field;
        public float originalValue;
    }

    private static readonly string[] SpeedFieldNames =
    {
        "movementSpeed",
        "chaseSpeed",
        "patrolSpeed",
        "dashSpeed",
        "moveSpeed",
        "speed"
    };

    [SerializeField] private Color frozenTint = new Color(0.65f, 0.9f, 1f, 1f);

    private readonly List<ModifiedSpeedField> modifiedFields = new List<ModifiedSpeedField>();
    private readonly List<SpriteRenderer> tintedRenderers = new List<SpriteRenderer>();
    private readonly List<Color> originalColors = new List<Color>();

    private float activeMultiplier = 1f;
    private float expireTime;
    private bool initialized;

    public void ApplySlow(float multiplier, float duration)
    {
        multiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        duration = Mathf.Max(0f, duration);

        if (!initialized)
            InitializeTargets();

        // Keep the strongest slow currently active.
        activeMultiplier = Mathf.Min(activeMultiplier, multiplier);
        expireTime = Mathf.Max(expireTime, Time.time + duration);

        ApplyCurrentSlow();
        ApplyTint();

        enabled = true;
    }

    private void Update()
    {
        if (Time.time < expireTime)
            return;

        RestoreOriginalValues();
        RestoreTint();
        Destroy(this);
    }

    private void InitializeTargets()
    {
        initialized = true;

        var components = GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];
            if (component == null || component == this)
                continue;

            string typeName = component.GetType().Name;
            if (typeName.Contains("Bullet"))
                continue;

            for (int f = 0; f < SpeedFieldNames.Length; f++)
            {
                FieldInfo field = component.GetType().GetField(
                    SpeedFieldNames[f],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field == null || field.FieldType != typeof(float))
                    continue;

                var entry = new ModifiedSpeedField
                {
                    component = component,
                    field = field,
                    originalValue = (float)field.GetValue(component)
                };

                modifiedFields.Add(entry);
            }
        }

        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            tintedRenderers.Add(renderers[i]);
            originalColors.Add(renderers[i].color);
        }
    }

    private void ApplyCurrentSlow()
    {
        for (int i = 0; i < modifiedFields.Count; i++)
        {
            var entry = modifiedFields[i];
            if (entry.component == null || entry.field == null)
                continue;

            entry.field.SetValue(entry.component, entry.originalValue * activeMultiplier);
        }
    }

    private void RestoreOriginalValues()
    {
        for (int i = 0; i < modifiedFields.Count; i++)
        {
            var entry = modifiedFields[i];
            if (entry.component == null || entry.field == null)
                continue;

            entry.field.SetValue(entry.component, entry.originalValue);
        }
    }

    private void ApplyTint()
    {
        for (int i = 0; i < tintedRenderers.Count; i++)
        {
            if (tintedRenderers[i] == null)
                continue;

            tintedRenderers[i].color = frozenTint;
        }
    }

    private void RestoreTint()
    {
        int count = Mathf.Min(tintedRenderers.Count, originalColors.Count);
        for (int i = 0; i < count; i++)
        {
            if (tintedRenderers[i] == null)
                continue;

            tintedRenderers[i].color = originalColors[i];
        }
    }

    private void OnDestroy()
    {
        // Ensure restoration if component is removed unexpectedly.
        RestoreOriginalValues();
        RestoreTint();
    }
}
