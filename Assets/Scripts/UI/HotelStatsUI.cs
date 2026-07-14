using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotelStatsUI : MonoBehaviour
{
    [Tooltip("The DayManager in the scene.")]
    public DayManager dayManager;

    [Tooltip("TextMeshPro text that shows the money amount.")]
    public TMP_Text moneyText;

    [Tooltip("Slider used as the rating bar (set Min=0, Max=10 in Inspector).")]
    public Slider ratingBar;

    [Tooltip("TextMeshPro text that shows the numeric rating.")]
    public TMP_Text ratingText;

    private void Update()
    {
        moneyText.text = $"₩ {dayManager.TotalMoney:N0}";
        ratingBar.value = dayManager.AverageRating;
        ratingText.text = $"{dayManager.AverageRating:F1}";
    }
}