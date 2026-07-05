using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 태블릿 패널 열기/닫기 컨트롤러.
    /// - Monitor 클릭 → 열림 + 진입 사운드
    /// - 패널 바깥(오버레이) 클릭 → 닫힘 + 종료 사운드
    /// </summary>
    public class TabletController : MonoBehaviour
    {
        [Header("Click Areas")]
        [SerializeField] private Collider2D monitorClickArea;
        [SerializeField] private Collider2D overlayClickArea;
        [SerializeField] private Collider2D tabletActiveArea;
        [SerializeField] private Collider2D mainMenuButton;   // 메뉴 패널 열기 버튼

        [Header("Show/Hide Targets")]
        [SerializeField] private GameObject overlayObj;
        [SerializeField] private GameObject mainMenuPanelObj;
        [SerializeField] private GameObject roomPanelObj;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("태블릿 열릴 때 사운드 (키보드 두드리는 느낌)")]
        [SerializeField] private AudioClip openSfx;
        [Tooltip("태블릿 닫힐 때 사운드 (옷 스치는 듯한 부드러운 음)")]
        [SerializeField] private AudioClip closeSfx;

        [Header("Audio Volumes")]
        [Range(0f, 1f)] [SerializeField] private float masterSfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float openVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float closeVolume = 1f;

        [Header("Behavior")]
        [SerializeField] private bool startOpen = false;

        public bool IsOpen { get; private set; }

        private Camera _cachedCam;

        private void Awake()
        {
            // Awake에서는 사운드 재생 안 함 (게임 시작 시 의도치 않은 재생 방지)
            IsOpen = startOpen;
            if (overlayObj != null) overlayObj.SetActive(startOpen);
            if (mainMenuPanelObj   != null) mainMenuPanelObj.SetActive(startOpen);
            if (roomPanelObj   != null) roomPanelObj.SetActive(startOpen);
        }

        public void Open()  => SetOpen(true,  true);
        public void Close() => SetOpen(false, true);
        public void Toggle() => SetOpen(!IsOpen, true);

        private void SetOpen(bool open, bool playSound)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            if (overlayObj != null) overlayObj.SetActive(open);
            if (mainMenuPanelObj   != null) mainMenuPanelObj.SetActive(open);
            if (roomPanelObj   != null) roomPanelObj.SetActive(false);

            if (playSound)
            {
                PlaySfx(open ? openSfx : closeSfx, open ? openVolume : closeVolume);
            }
        }

private void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (sfxSource != null && clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * masterSfxVolume));
        }

        private void Update()
        {
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;

            Vector2 mousePos;
            bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            if (!mouseDown) return;

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 worldPoint = new Vector2(w3.x, w3.y);

            if (!IsOpen)
            {
                if (monitorClickArea != null && monitorClickArea.OverlapPoint(worldPoint))
                    Open();
            }
            else
            {
                if (mainMenuButton != null && mainMenuButton.OverlapPoint(worldPoint))
                {
                    if (roomPanelObj != null && roomPanelObj.activeSelf)
                    {
                        roomPanelObj.SetActive(false);
                        if (mainMenuPanelObj != null) mainMenuPanelObj.SetActive(true);
                        return;
                    }
                }

                bool onPanel = tabletActiveArea != null && tabletActiveArea.OverlapPoint(worldPoint);
                bool onOverlay = overlayClickArea != null && overlayClickArea.OverlapPoint(worldPoint);
                if (onOverlay && !onPanel) Close();
            }
        }

        private static void GetMouseInput(out Vector2 pos, out bool downThisFrame)
        {
            pos = Vector2.zero;
            downThisFrame = false;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                pos = mouse.position.ReadValue();
                downThisFrame = mouse.leftButton.wasPressedThisFrame;
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pos = Input.mousePosition;
            downThisFrame = Input.GetMouseButtonDown(0);
#endif
        }
    }
}
