using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 태블릿 뒤로가기 버튼.
    /// 클릭하면 TabletUI.GoBack() 호출하여 메인 메뉴로 돌아간다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class TabletBackButton : MonoBehaviour
    {
        [SerializeField] private TabletUI tabletUI;

        private Camera _cachedCam;

        private void Update()
        {
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;

            Vector2 mousePos;
            bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            if (!mouseDown) return;

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 wp = new Vector2(w3.x, w3.y);

            var col = GetComponent<BoxCollider2D>();
            if (col != null && col.OverlapPoint(wp) && tabletUI != null)
            {
                tabletUI.GoBack();
            }
        }

        private static void GetMouseInput(out Vector2 pos, out bool downThisFrame)
        {
            pos = Vector2.zero; downThisFrame = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) { pos = mouse.position.ReadValue(); downThisFrame = mouse.leftButton.wasPressedThisFrame; return; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pos = Input.mousePosition; downThisFrame = Input.GetMouseButtonDown(0);
#endif
        }
    }
}
