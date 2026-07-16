using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 태블릿 메인 메뉴 버튼. 클릭 시 TabletUI에 페이지 전환 요청.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TabletMenuButton : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string pageKey = "reservation";
        [SerializeField] private TabletUI tabletUI;

        [Header("오디오")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip clickSfx;
        [Range(0f, 1f)] [SerializeField] private float masterSfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float clickVolume = 1f;

        private Camera _cachedCam;

        private void Update()
        {
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null || tabletUI == null || !tabletUI.CanUseMainMenuButtons()) return;

            Vector2 mousePos; bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            if (!mouseDown) return;

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 wp = new Vector2(w3.x, w3.y);

            var col = GetComponent<BoxCollider2D>();
            if (col != null && col.OverlapPoint(wp))
            {
                if (sfxSource != null && clickSfx != null) sfxSource.PlayOneShot(clickSfx, Mathf.Clamp01(clickVolume * masterSfxVolume));
                tabletUI.OnMenuButtonClicked(pageKey);
            }
        }

        private static void GetMouseInput(out Vector2 pos, out bool downThisFrame)
        {
            pos = Vector2.zero; downThisFrame = false;
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null)
            {
                pos = pointer.position.ReadValue();
                downThisFrame = pointer.press.wasPressedThisFrame;
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pos = Input.mousePosition; downThisFrame = Input.GetMouseButtonDown(0);
#endif
        }
    }
}
