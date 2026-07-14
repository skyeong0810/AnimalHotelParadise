using UnityEngine;

namespace AnimalHotel.Counter
{
    [RequireComponent(typeof(Collider2D))]
    public class MemoOptionButton : MonoBehaviour
    {
        private RoomUI _roomUI;
        private Camera _cachedCam;
        private SpriteRenderer _spriteRenderer;

        public void Initialize(RoomUI roomUI)
        {
            _roomUI = roomUI;
            if (_roomUI == null)
            {
                _roomUI = FindFirstObjectByType<RoomUI>();
            }
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (_roomUI == null)
            {
                _roomUI = FindFirstObjectByType<RoomUI>();
            }
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void Update()
        {
            if (_roomUI == null)
            {
                _roomUI = FindFirstObjectByType<RoomUI>();
            }
            if (_roomUI == null) return;
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;

            Vector2 mousePos; bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            if (!mouseDown) return;

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            var col = GetComponent<Collider2D>();
            if (col != null && col.OverlapPoint(new Vector2(w3.x, w3.y)))
            {
                if (_spriteRenderer != null && _spriteRenderer.sprite != null)
                {
                    _roomUI.SetSelectedRoomMemoSprite(_spriteRenderer.sprite);
                }
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
