using UnityEngine;

namespace AnimalHotel.Counter
{
    [RequireComponent(typeof(Collider2D))]
    public class RoomMenuButton : MonoBehaviour
    {
        private enum RoomAction
        {
            Assign,
            Clean,
            AdvancedClean,
            Memo
        }

        [SerializeField] private RoomUI roomUI;
        [SerializeField] private RoomAction action = RoomAction.Assign;


        private Camera _cachedCam;
        private bool _isMemoOpen = false;

        private void Start()
        {
            Debug.Log($"[RoomMenuButton] Start called on {gameObject.name}. Action: {action}");
            if (action == RoomAction.Memo)
            {
                if (transform.childCount > 0)
                {
                    _isMemoOpen = transform.GetChild(0).gameObject.activeSelf;
                }
                InitializeMemoChildren();
            }
        }

        private void InitializeMemoChildren()
        {
            // Find all SpriteRenderers in children recursively (including inactive ones)
            var childSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            Debug.Log($"[RoomMenuButton] InitializeMemoChildren found {childSpriteRenderers.Length} sprite renderers in {gameObject.name}.");

            foreach (var sr in childSpriteRenderers)
            {
                if (sr.gameObject == gameObject) continue; // Skip parent

                string objName = sr.gameObject.name;
                // Skip non-animal sprite objects
                if (objName == "Background" || objName == "MemoTab")
                {
                    Debug.Log($"[RoomMenuButton] Skipping initialization for container/background object: {objName}");
                    continue;
                }

                var box = sr.gameObject.GetComponent<BoxCollider2D>();
                if (box == null)
                {
                    box = sr.gameObject.AddComponent<BoxCollider2D>();
                }

                // Size box collider to match the sprite's local bounds
                box.size = sr.localBounds.size;
                box.offset = sr.localBounds.center;

                var memoOpt = sr.gameObject.GetComponent<MemoOptionButton>();
                if (memoOpt == null)
                {
                    memoOpt = sr.gameObject.AddComponent<MemoOptionButton>();
                }
                memoOpt.Initialize(roomUI);
                Debug.Log($"[RoomMenuButton] Initialized animal sprite: {objName} with BoxCollider2D and MemoOptionButton.");
            }
        }

        private void Update()
        {
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;
            if (action != RoomAction.Memo && roomUI == null) return;

            Vector2 mousePos; bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            if (!mouseDown) return;

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            if (!GetComponent<Collider2D>().OverlapPoint(new Vector2(w3.x, w3.y))) return;

            Debug.Log($"[RoomMenuButton] Clicked parent button: {gameObject.name}, Action: {action}, isMemoOpen: {_isMemoOpen}");

            if (action == RoomAction.Memo && _isMemoOpen)
            {
                bool clickedChild = false;
                var childColliders = GetComponentsInChildren<Collider2D>(false); // Only active ones
                foreach (var col in childColliders)
                {
                    if (col.gameObject == gameObject) continue; // Skip parent
                    if (col.gameObject.name == "Background") continue; // Background clicks still toggle parent

                    if (col.OverlapPoint(new Vector2(w3.x, w3.y)))
                    {
                        Debug.Log($"[RoomMenuButton] Click intercepted by child animal sprite: {col.gameObject.name}. Ignoring parent toggle.");
                        clickedChild = true;
                        break;
                    }
                }
                if (clickedChild) return;
            }

            switch (action)
            {
                case RoomAction.Clean:
                    roomUI.OnCleanButtonClicked();
                    break;
                case RoomAction.AdvancedClean:
                    roomUI.OnAdvancedCleanButtonClicked();
                    break;
                case RoomAction.Memo:
                    ToggleChildren();
                    break;
                default:
                    roomUI.OnAssignButtonClicked();
                    break;
            }
        }

        private void ToggleChildren()
        {
            _isMemoOpen = !_isMemoOpen;
            Debug.Log($"[RoomMenuButton] Toggling memo menu. New state: {_isMemoOpen}");
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(_isMemoOpen);
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