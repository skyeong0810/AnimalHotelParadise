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
            Memo,
            MemoDelete
        }

        [SerializeField] private RoomUI roomUI;
        [SerializeField] private RoomAction action = RoomAction.Assign;


        private Camera _cachedCam;
        private bool _isMemoOpen = false;

        private void Start()
        {
            if (action == RoomAction.Memo)
            {
                if (transform.childCount > 0)
                {
                    _isMemoOpen = transform.GetChild(0).gameObject.activeSelf;
                }
                InitializeMemoChildren();
            }
        }

        private void OnDisable()
        {
            if (action == RoomAction.Memo)
            {
                if (transform.childCount > 0)
                {
                    transform.GetChild(0).gameObject.SetActive(false);
                }
                _isMemoOpen = false;
            }
        }

        private void InitializeMemoChildren()
        {
            // Find all SpriteRenderers in children recursively (including inactive ones)
            var childSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            int initializedCount = 0;

            foreach (var sr in childSpriteRenderers)
            {
                if (sr.gameObject == gameObject) continue; // Skip parent

                // ONLY consider GameObjects that have a Collider2D component
                var col = sr.gameObject.GetComponent<Collider2D>();
                if (col == null) continue;

                string objName = sr.gameObject.name;
                // Skip non-animal sprite objects
                if (objName == "Background" || objName == "MemoTab")
                {
                    continue;
                }

                var memoOpt = sr.gameObject.GetComponent<MemoOptionButton>();
                if (memoOpt == null)
                {
                    memoOpt = sr.gameObject.AddComponent<MemoOptionButton>();
                }
                memoOpt.Initialize(roomUI);
                initializedCount++;
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

            if (action == RoomAction.Memo && _isMemoOpen)
            {
                bool clickedChild = false;
                var childColliders = GetComponentsInChildren<Collider2D>(false); // Only active ones
                foreach (var col in childColliders)
                {
                    if (col.gameObject == gameObject) continue; // Skip parent
                    if (col.gameObject.name == "Background" || col.gameObject.name == "MemoTab") continue;

                    if (col.OverlapPoint(new Vector2(w3.x, w3.y)))
                    {
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
                case RoomAction.MemoDelete:
                    roomUI.OnMemoDeleteButtonClicked();
                    break;
                default:
                    roomUI.OnAssignButtonClicked();
                    break;
            }
        }

        private void ToggleChildren()
        {
            _isMemoOpen = !_isMemoOpen;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(_isMemoOpen);
            }
        }

        private static void GetMouseInput(out Vector2 pos, out bool downThisFrame)
        {
            AnimalHotel.InputHelper.GetInput(out pos, out downThisFrame);
        }
    }
}