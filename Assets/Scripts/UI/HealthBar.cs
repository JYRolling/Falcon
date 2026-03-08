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

    public void SetMaxHealth(int health)
    {
        if (slider == null || fill == null)
            return;

        slider.maxValue = health;
        slider.value = health;

        fill.color = gradient.Evaluate(1f);
    }

    public void SetHealth(int health)
    {
        if (slider == null || fill == null)
            return;

        slider.value = health;
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }
}
