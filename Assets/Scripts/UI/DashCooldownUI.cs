using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// แสดง UI cooldown ของ Dash โดยใช้ Image แบบ Radial Fill
/// วิธีตั้งค่าใน Unity:
///   1. สร้าง UI Image ใน Canvas (ตั้งค่า Image Type = Filled, Fill Method = Radial 360)
///   2. ลาก script นี้ใส่ GameObject ใดก็ได้ใน Scene
///   3. กำหนด CooldownFillImage = Image ที่สร้างไว้
///   4. กำหนด Player = GameObject ของ Player
/// </summary>
public class DashCooldownUI : MonoBehaviour
{
    [Tooltip("Image ที่ใช้แสดง cooldown (Image Type = Filled, Fill Method = Radial 360)")]
    [SerializeField] private Image cooldownFillImage;

    [Tooltip("Player GameObject ที่มี PlayerController")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("สีตอน dash พร้อมใช้")]
    [SerializeField] private Color readyColor = Color.white;

    [Tooltip("สีตอน cooldown")]
    [SerializeField] private Color cooldownColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private void Start()
    {
        if (cooldownFillImage == null)
            Debug.LogError("[DashCooldownUI] ยังไม่ได้กำหนด CooldownFillImage!");

        if (playerController == null)
            Debug.LogError("[DashCooldownUI] ยังไม่ได้กำหนด PlayerController!");
    }

    private void Update()
    {
        if (cooldownFillImage == null || playerController == null) return;

        float percent = playerController.GetDashCooldownPercent();
        cooldownFillImage.fillAmount = percent;
        cooldownFillImage.color = percent >= 1f ? readyColor : cooldownColor;
    }
}
