using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 태블릿 패널 열기/닫기 컨트롤러.
    /// - Monitor 클릭 → 열림 (오버레이 + 패널 활성화)
    /// - 패널 바깥(오버레이만 있는 영역) 클릭 → 닫힘
    /// 새 Input System / 옛 Input Manager 둘 다 지원.
    /// </summary>
    public class TabletController : MonoBehaviour
    {
        [Header("Click Areas")]
        [Tooltip("닫혔을 때 클릭하면 태블릿이 열리는 영역 (보통 Monitor의 Collider2D)")]
        [SerializeField] private Collider2D monitorClickArea;

        [Tooltip("패널 내부 영역. 여기 클릭은 닫기 신호로 처리하지 않음.")]
        [SerializeField] private Collider2D panelClickArea;

        [Tooltip("화면 전체를 덮는 오버레이. 여기 클릭(패널 바깥) = 닫기.")]
        [SerializeField] private Collider2D overlayClickArea;

        [Header("Show/Hide Targets")]
        [SerializeField] private GameObject overlayObj;
        [SerializeField] private GameObject panelObj;

        [Header("Behavior")]
        [SerializeField] private bool startOpen = false;

        public bool IsOpen { get; private set; }

        private Camera _cachedCam;

        private void Awake()
        {
            SetOpen(startOpen);
        }

        public void Open()  => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!IsOpen);

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (overlayObj != null) overlayObj.SetActive(open);
            if (panelObj   != null) panelObj.SetActive(open);
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
                bool onPanel   = panelClickArea   != null && panelClickArea.OverlapPoint(worldPoint);
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
