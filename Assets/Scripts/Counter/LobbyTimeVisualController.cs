using UnityEngine;

public class LobbyTimeVisualController : MonoBehaviour
{
    [SerializeField] private DayManager dayManager;
    [SerializeField] private SpriteRenderer dayBackground;
    [SerializeField] private SpriteRenderer nightBackground;
    [SerializeField] private SpriteRenderer dayTable;
    [SerializeField] private SpriteRenderer nightTable;
    [SerializeField] private bool defaultToDay = true;

    private void Awake()
    {
        EnsureDayManager();
        Refresh();
    }

    private void OnEnable()
    {
        EnsureDayManager();

        if (dayManager != null)
        {
            dayManager.OnPhaseChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (dayManager != null)
        {
            dayManager.OnPhaseChanged -= Refresh;
        }
    }

    public void Configure(DayManager manager, SpriteRenderer dayBg, SpriteRenderer nightBg, SpriteRenderer dayTableRenderer, SpriteRenderer nightTableRenderer)
    {
        dayManager = manager;
        dayBackground = dayBg;
        nightBackground = nightBg;
        dayTable = dayTableRenderer;
        nightTable = nightTableRenderer;
        Refresh();
    }

    public void Refresh()
    {
        bool showDay = dayManager != null ? dayManager.IsMorning : defaultToDay;

        SetVisible(dayBackground, showDay);
        SetVisible(nightBackground, !showDay);
        SetVisible(dayTable, showDay);
        SetVisible(nightTable, !showDay);
    }

    private void EnsureDayManager()
    {
        if (dayManager == null)
        {
            dayManager = FindFirstObjectByType<DayManager>();
        }
    }

    private static void SetVisible(SpriteRenderer renderer, bool visible)
    {
        if (renderer != null)
        {
            renderer.gameObject.SetActive(visible);
        }
    }
}
