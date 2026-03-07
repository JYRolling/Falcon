using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField]
    private Slider slider;
    [SerializeField]
    private Gradient gradient;
    [SerializeField]
    private Image fill;

    private void Awake()
    {
        // Try to auto-find a Slider if not assigned
        if (slider == null)
            slider = GetComponent<Slider>() ?? GetComponentInChildren<Slider>();

        // Try to auto-find the fill Image from the slider if possible
        if (fill == null && slider != null && slider.fillRect != null)
            fill = slider.fillRect.GetComponent<Image>();

        // Final checks and warnings
        if (slider == null)
            Debug.LogError("[HealthBar] Slider reference is missing on " + gameObject.name);
        if (fill == null)
            Debug.LogError("[HealthBar] Fill Image reference is missing on " + gameObject.name);

        // ensure sensible slider defaults
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.wholeNumbers = false;
        }
    }

#if UNITY_EDITOR
    // Keep inspector changes consistent while editing
    private void OnValidate()
    {
        if (slider == null)
            slider = GetComponent<Slider>() ?? GetComponentInChildren<Slider>();
        if (fill == null && slider != null && slider.fillRect != null)
            fill = slider.fillRect.GetComponent<Image>();
    }
#endif

    // Use floats so fractional health changes are visible
    public void SetMaxHealth(float health)
    {
        if (slider == null || fill == null)
        {
            Debug.LogWarning($"[HealthBar] SetMaxHealth skipped because slider or fill is null on '{gameObject.name}'");
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = health;
        slider.value = health;

        // Force layout/UI update to make certain Unity repaints the UI immediately
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(slider.GetComponent<RectTransform>());

        if (gradient != null)
            fill.color = gradient.Evaluate(1f);

        Debug.Log($"[HealthBar] SetMaxHealth on '{gameObject.name}': min={slider.minValue} max={slider.maxValue} value={slider.value} fill={(fill!=null?fill.name:"null")} active={gameObject.activeInHierarchy}");
    }

    public void SetHealth(float health)
    {
        if (slider == null || fill == null)
        {
            Debug.LogWarning($"[HealthBar] SetHealth skipped because slider or fill is null on '{gameObject.name}'");
            return;
        }

        slider.value = Mathf.Clamp(health, slider.minValue, slider.maxValue);

        // Force layout/UI update to make certain Unity repaints the UI immediately
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(slider.GetComponent<RectTransform>());

        if (gradient != null)
            fill.color = gradient.Evaluate(slider.normalizedValue);

        Debug.Log($"[HealthBar] SetHealth on '{gameObject.name}': value={slider.value} normalized={slider.normalizedValue} active={gameObject.activeInHierarchy}");
    }
}
