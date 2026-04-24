using System.Collections;
using UnityEngine;

public class DamageBlink : MonoBehaviour
{
    [Header("Damage Blink")]
    [SerializeField] private int damageBlinkCount = 4;
    [SerializeField] private float damageBlinkInterval = 0.06f;
    [SerializeField, Range(0f, 1f)] private float damageBlinkMinAlpha = 0.25f;
    [SerializeField] private bool useRealtime = true;

    private SpriteRenderer[] spriteRenderers;
    private Color[] baseSpriteColors;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        CacheBaseSpriteColors();
    }

    public void TriggerBlink()
    {
        if (!isActiveAndEnabled)
            return;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            CacheBaseSpriteColors();
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        if (blinkCoroutine != null)
        {
            RestoreBaseSpriteColors();
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        blinkCoroutine = StartCoroutine(DamageBlinkRoutine());
    }

    private IEnumerator DamageBlinkRoutine()
    {
        int blinkCount = Mathf.Max(1, damageBlinkCount);
        float interval = Mathf.Max(0.01f, damageBlinkInterval);

        if (baseSpriteColors == null || baseSpriteColors.Length != spriteRenderers.Length)
            CacheBaseSpriteColors();

        for (int i = 0; i < blinkCount; i++)
        {
            SetSpriteAlpha(damageBlinkMinAlpha);
            yield return useRealtime ? new WaitForSecondsRealtime(interval) : new WaitForSeconds(interval);

            SetSpriteAlpha(1f);
            yield return useRealtime ? new WaitForSecondsRealtime(interval) : new WaitForSeconds(interval);
        }

        RestoreBaseSpriteColors();
        blinkCoroutine = null;
    }

    private void SetSpriteAlpha(float alphaScale)
    {
        float clampedScale = Mathf.Clamp01(alphaScale);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            var sr = spriteRenderers[i];
            if (sr == null) continue;

            Color c = baseSpriteColors[i];
            c.a = baseSpriteColors[i].a * clampedScale;
            sr.color = c;
        }
    }

    private void CacheBaseSpriteColors()
    {
        if (spriteRenderers == null)
        {
            baseSpriteColors = null;
            return;
        }

        baseSpriteColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                baseSpriteColors[i] = spriteRenderers[i].color;
        }
    }

    private void RestoreBaseSpriteColors()
    {
        if (spriteRenderers == null || baseSpriteColors == null) return;

        int count = Mathf.Min(spriteRenderers.Length, baseSpriteColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = baseSpriteColors[i];
        }
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        RestoreBaseSpriteColors();
    }
}