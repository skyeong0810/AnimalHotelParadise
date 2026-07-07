using System.Collections.Generic;
using UnityEngine;

namespace AnimalHotel.Counter
{
    public class RoomUI : MonoBehaviour
    {
        [Header("data")]
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private CounterFlow counterFlow;
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private DayManager dayManager;

        [Header("cleaning_time")]
        [Min(0f)]
        [SerializeField] private float normalCleanDurationSeconds = 0f;

        [Header("reservation_list")]
        [SerializeField] private Transform background;
        [SerializeField] private float spacing = 1.2f;
        [SerializeField] private float scale = 1.0f;
        [SerializeField] private float reservationIconWorldSize = 0.7f;

        [Header("room_buttons")]
        [SerializeField] private List<SpriteRenderer> roomRenderers;
        [SerializeField] private Sprite vacantSprite;
        [SerializeField] private Sprite selectedSprite;

        [Header("assign_button")]
        [SerializeField] private SpriteRenderer assignButtonRenderer;
        [SerializeField] private SpriteRenderer cleanButtonRenderer;
        [SerializeField] private SpriteRenderer advancedCleanButtonRenderer;

        [SerializeField] private Color assignButtonActiveColor = Color.white;
        [SerializeField] private Color assignButtonInactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("room_status_colors")]
        [SerializeField] private Color vacantRoomColor = Color.white;
        [SerializeField] private Color occupiedRoomColor = Color.white;
        [SerializeField] private Color needsExaminationRoomColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color needsCleaningRoomColor = new Color(0.45f, 0.8f, 1f, 1f);
        [SerializeField] private Color advancedCleaningInProgressRoomColor = new Color(0.7f, 0.45f, 1f, 1f);


        public event System.Action OnRoomAssigned;

        private int _selectedRoomNumber = -1;

        private void Awake()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
            InitializeRoomRenderers();
        }

        private void OnEnable()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
            if (dayManager != null) dayManager.OnPhaseChanged += RefreshRoomGrid;
        }

        private void OnDisable()
        {
            if (dayManager != null) dayManager.OnPhaseChanged -= RefreshRoomGrid;
        }

        private void InitializeRoomRenderers()
        {
            if (roomRenderers == null || roomRenderers.Count == 0)
            {
                var buttons = FindObjectsByType<RoomButton>(FindObjectsSortMode.None);
                roomRenderers = new List<SpriteRenderer>();
                for (int i = 0; i < 10; i++)
                {
                    roomRenderers.Add(null);
                }

                foreach (var btn in buttons)
                {
                    if (btn != null && btn.roomNumber >= 1 && btn.roomNumber <= 10)
                    {
                        var sr = btn.GetComponent<SpriteRenderer>();
                        roomRenderers[btn.roomNumber - 1] = sr;
                    }
                }
            }

            if (vacantSprite == null && roomRenderers != null)
            {
                foreach (var sr in roomRenderers)
                {
                    if (sr != null && sr.sprite != null)
                    {
                        vacantSprite = sr.sprite;
                        break;
                    }
                }
            }
        }

        /// <summary>Called by TabletUI when the room page is opened.</summary>
        public void OnPageOpened()
        {
            _selectedRoomNumber = -1;
            RefreshRoomGrid();
        }

        public void OnRoomClicked(int roomNumber)
        {
            _selectedRoomNumber = roomNumber;
            RefreshRoomGrid();
        }

        public void OnAssignButtonClicked()
        {
            if (roomManager == null) return;
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected.");
                return;
            }

            var selectedRoom = roomManager.GetRoom(_selectedRoomNumber);
            if (selectedRoom.status != RoomStatus.Vacant)
            {
                Debug.LogWarning($"[RoomUI] Room {_selectedRoomNumber} is not vacant.");
                RefreshRoomGrid();
                return;
            }

            if (counterFlow == null) return;
            var guest = counterFlow.GetCurrentGuest();
            if (guest == null)
            {
                Debug.LogWarning("[RoomUI] No current guest to assign.");
                return;
            }

            var existingRoom = roomManager.GetRoomByOccupant(guest);
            if (existingRoom != null)
            {
                Debug.LogWarning($"[RoomUI] {guest.guestName} is already assigned to room {existingRoom.roomNumber}.");
                RefreshRoomGrid();
                return;
            }

            bool success = roomManager.AssignRoom(_selectedRoomNumber, guest);
            if (success)
            {
                _selectedRoomNumber = -1;
                RefreshRoomGrid();
                OnRoomAssigned?.Invoke();
                if (dialogueManager != null) dialogueManager.NotifyRoomAssigned();
            }
        }

        public void OnCleanButtonClicked()
        {
            if (roomManager == null) return;
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected.");
                return;
            }

            bool cleaned = roomManager.CleanRoom(_selectedRoomNumber);
            if (cleaned)
            {
                SpendNormalCleaningTime();
            }
            RefreshRoomGrid();
        }

        public void OnAdvancedCleanButtonClicked()
        {
            if (roomManager == null) return;
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected.");
                return;
            }

            roomManager.AdvancedCleanRoom(_selectedRoomNumber);
            RefreshRoomGrid();
        }

        private void SpendNormalCleaningTime()
        {
            if (normalCleanDurationSeconds <= 0f) return;
            if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
            if (dayManager != null)
            {
                dayManager.SpendPhaseTime(normalCleanDurationSeconds, "Normal cleaning");
            }
        }

        private void RefreshRoomGrid()
        {
            if (roomManager == null || roomRenderers == null) return;
            for (int i = 0; i < roomRenderers.Count; i++)
            {
                var room = roomManager.GetRoom(i + 1);
                var sr = roomRenderers[i];
                if (sr == null) continue;

                sr.sprite = GetRoomSprite(room, i + 1 == _selectedRoomNumber);
                sr.color = GetRoomColor(room);

                // Handle the occupant sprite overlay
                Transform occupantTransform = sr.transform.Find("RoomOccupantSprite");
                if (room != null && room.status == RoomStatus.Occupied && room.occupant != null && room.occupant.species != null && room.occupant.species.speciesSprite != null)
                {
                    GameObject occupantObj;
                    SpriteRenderer occupantSr;
                    if (occupantTransform != null)
                    {
                        occupantObj = occupantTransform.gameObject;
                        occupantSr = occupantObj.GetComponent<SpriteRenderer>();
                    }
                    else
                    {
                        occupantObj = new GameObject("RoomOccupantSprite");
                        occupantObj.transform.SetParent(sr.transform);
                        occupantSr = occupantObj.AddComponent<SpriteRenderer>();
                    }

                    if (occupantSr != null)
                    {
                        occupantSr.gameObject.SetActive(true);
                        occupantSr.sprite = room.occupant.species.speciesSprite;
                        occupantSr.sortingLayerID = sr.sortingLayerID;
                        occupantSr.sortingLayerName = sr.sortingLayerName;
                        occupantSr.sortingOrder = sr.sortingOrder + 5; // Render above room button background

                        occupantObj.transform.localPosition = Vector3.zero;

                        // Size the occupant sprite overlay nicely inside the room
                        Vector3 parentWorldScale = sr.transform.lossyScale;
                        float targetWorldScale = 0.12f; // Fallback
                        string spriteNameLower = room.occupant.species.speciesSprite.name.ToLower();
                        if (spriteNameLower.Contains("squirrel") || spriteNameLower.Contains("mouse"))
                        {
                            targetWorldScale = 0.17f;
                        }
                        else if (spriteNameLower.Contains("roedeer"))
                        {
                            targetWorldScale = 0.03f;
                        }
                        else if (spriteNameLower.Contains("rabbit"))
                        {
                            targetWorldScale = 0.14f;
                        }

                        occupantObj.transform.localScale = new Vector3(
                            parentWorldScale.x != 0f ? (targetWorldScale / parentWorldScale.x) : targetWorldScale,
                            parentWorldScale.y != 0f ? (targetWorldScale / parentWorldScale.y) : targetWorldScale,
                            1f
                        );
                    }
                }
                else
                {
                    if (occupantTransform != null)
                    {
                        occupantTransform.gameObject.SetActive(false);
                    }
                }
            }
            RefreshAssignButton();
            RefreshReservationList();
        }

        private void RefreshReservationList()
        {
            if (background == null) return;

            // Clear existing icons safely (looping backwards to handle child count changes)
            for (int i = background.childCount - 1; i >= 0; i--)
            {
                Transform child = background.GetChild(i);
                if (child != null)
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            if (dayManager == null)
            {
                dayManager = FindFirstObjectByType<DayManager>();
            }
            if (dayManager == null) return;

            // Gather all reservation guests from today's arrivals
            List<Animal> reservationGuests = new List<Animal>();
            if (dayManager.TodaysArrivals != null)
            {
                foreach (var a in dayManager.TodaysArrivals)
                {
                    if (a.hasReservation && !reservationGuests.Contains(a))
                        reservationGuests.Add(a);
                }
            }

            Debug.Log($"[RoomUI] RefreshReservationList found {reservationGuests.Count} reservation guests.");

            // Instantiate sprites horizontally from right to left
            float localRightEdge = 0.5f;
            SpriteRenderer bgSr = background.GetComponent<SpriteRenderer>();
            if (bgSr != null && bgSr.sprite != null)
            {
                localRightEdge = bgSr.sprite.bounds.max.x;
            }

            Vector3 backgroundLossyScale = background.lossyScale;
            float parentScaleX = backgroundLossyScale.x != 0f ? backgroundLossyScale.x : 1f;
            float targetIconWorldSize = GetReservationIconWorldSize();

            // Convert target world scale, spacing, and margin to local X units
            float localIconWidth = parentScaleX != 0f ? (targetIconWorldSize / parentScaleX) : targetIconWorldSize;
            float localSpacing = parentScaleX != 0f ? (spacing / parentScaleX) : spacing;
            float localMargin = parentScaleX != 0f ? (0.2f / parentScaleX) : 0.2f; // 0.2f world units margin

            // Total local width of the background is 2 * localRightEdge (assuming symmetric)
            float totalLocalWidth = 2f * localRightEdge;
            float maxAvailableLocalWidth = totalLocalWidth - localIconWidth - 2f * localMargin;

            // Dynamically shrink local spacing if they don't fit
            float actualLocalSpacing = localSpacing;
            int numIcons = reservationGuests.Count;
            if (numIcons > 1 && maxAvailableLocalWidth > 0f)
            {
                float requiredLocalWidth = (numIcons - 1) * localSpacing;
                if (requiredLocalWidth > maxAvailableLocalWidth)
                {
                    actualLocalSpacing = maxAvailableLocalWidth / (numIcons - 1);
                }
            }

            // First icon (i = 0) is at the left edge
            float startLocalX = -localRightEdge + (0.5f * localIconWidth) + localMargin;

            for (int i = 0; i < numIcons; i++)
            {
                var guest = reservationGuests[i];
                Sprite iconSprite = guest.ReservationIconSprite;
                if (iconSprite == null) continue;

                GameObject iconObj = new GameObject("ReservationIcon_" + guest.guestName);
                iconObj.transform.SetParent(background);

                // Position: start from left (startLocalX) and move right (+ i * actualLocalSpacing)
                float posX = startLocalX + (i * actualLocalSpacing);

                iconObj.transform.localPosition = new Vector3(posX, 0f, 0f);

                Vector3 iconLocalScale = GetReservationIconLocalScale(iconSprite, backgroundLossyScale, targetIconWorldSize);
                iconObj.transform.localScale = iconLocalScale;
                Vector3 centerOffset = Vector3.Scale(iconSprite.bounds.center, iconLocalScale);
                iconObj.transform.localPosition = new Vector3(posX, 0f, 0f) - centerOffset;
                iconObj.transform.localRotation = Quaternion.identity;

                SpriteRenderer sr = iconObj.AddComponent<SpriteRenderer>();
                sr.sprite = iconSprite;
                sr.sortingOrder = 60; // Make sure it's visible on top of panels
            }
        }

        private float GetReservationIconWorldSize()
        {
            float baseSize = reservationIconWorldSize > 0f ? reservationIconWorldSize : scale;
            return baseSize * 2.0f;
        }

        private Vector3 GetReservationIconLocalScale(Sprite sprite, Vector3 parentLossyScale, float targetWorldSize)
        {
            if (sprite == null) return Vector3.one;

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return Vector3.one;

            float parentScaleX = Mathf.Abs(parentLossyScale.x) > 0.0001f ? Mathf.Abs(parentLossyScale.x) : 1f;
            float parentScaleY = Mathf.Abs(parentLossyScale.y) > 0.0001f ? Mathf.Abs(parentLossyScale.y) : 1f;
            float targetSize = Mathf.Max(0.01f, targetWorldSize);

            float worldScaleX = targetSize / spriteSize.x;
            float worldScaleY = targetSize / spriteSize.y;
            float uniformWorldScale = Mathf.Min(worldScaleX, worldScaleY);

            return new Vector3(uniformWorldScale / parentScaleX, uniformWorldScale / parentScaleY, 1f);
        }

        private void RefreshAssignButton()
        {
            RoomData selectedRoom = null;
            if (roomManager != null && _selectedRoomNumber != -1)
            {
                selectedRoom = roomManager.GetRoom(_selectedRoomNumber);
            }

            var guest = counterFlow != null ? counterFlow.GetCurrentGuest() : null;
            bool canAssign = selectedRoom != null
                && selectedRoom.status == RoomStatus.Vacant
                && guest != null
                && roomManager.GetRoomByOccupant(guest) == null;
            bool canClean = selectedRoom != null && selectedRoom.status == RoomStatus.NeedsExamination;
            bool canAdvancedClean = IsMaintenanceRoom(selectedRoom);

            SetButtonColor(assignButtonRenderer, canAssign);
            SetButtonColor(cleanButtonRenderer, canClean);
            SetButtonColor(advancedCleanButtonRenderer, canAdvancedClean);
        }

        private void SetButtonColor(SpriteRenderer buttonRenderer, bool isActive)
        {
            if (buttonRenderer == null) return;
            buttonRenderer.color = isActive ? assignButtonActiveColor : assignButtonInactiveColor;
        }


        private Sprite GetRoomSprite(RoomData room, bool isSelected)
        {
            if (isSelected && selectedSprite != null) return selectedSprite;
            return vacantSprite;
        }

        private Color GetRoomColor(RoomData room)
        {
            if (room == null) return vacantRoomColor;
            switch (room.status)
            {
                case RoomStatus.Occupied:
                    return occupiedRoomColor;
                case RoomStatus.NeedsExamination:
                    return needsExaminationRoomColor;
                case RoomStatus.NeedsCleaning:
                    return needsCleaningRoomColor;
                case RoomStatus.AdvancedCleaningInProgress:
                    return advancedCleaningInProgressRoomColor;
                default:
                    return vacantRoomColor;
            }
        }

        private bool IsMaintenanceRoom(RoomData room)
        {
            return room != null
                && (room.status == RoomStatus.NeedsExamination || room.status == RoomStatus.NeedsCleaning);
        }

        public void SetSelectedRoomMemoSprite(Sprite memoSprite)
        {
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected to assign memo sprite.");
                return;
            }

            if (roomRenderers == null || _selectedRoomNumber < 1 || _selectedRoomNumber > roomRenderers.Count)
            {
                Debug.LogWarning("[RoomUI] Selected room number out of range.");
                return;
            }

            var roomRenderer = roomRenderers[_selectedRoomNumber - 1];
            if (roomRenderer == null)
            {
                Debug.LogWarning("[RoomUI] Selected room renderer is null.");
                return;
            }

            // Find or create the child GameObject for the memo sprite
            Transform memoTransform = roomRenderer.transform.Find("RoomMemoSprite");
            GameObject memoObj;
            SpriteRenderer memoSr;

             if (memoTransform != null)
            {
                memoObj = memoTransform.gameObject;
                memoSr = memoObj.GetComponent<SpriteRenderer>();
            }
            else
            {
                memoObj = new GameObject("RoomMemoSprite");
                memoObj.transform.SetParent(roomRenderer.transform);
                memoSr = memoObj.AddComponent<SpriteRenderer>();
            }

             if (memoSr != null)
            {
                memoSr.sprite = memoSprite;
                memoSr.sortingLayerID = roomRenderer.sortingLayerID;
                memoSr.sortingLayerName = roomRenderer.sortingLayerName;
                memoSr.sortingOrder = roomRenderer.sortingOrder + 10;
                
                // Prevent stretching by neutralizing the parent's lossy (world) scale.
                // We target a uniform world scale of 0.2f for the memo icon.
                Vector3 parentWorldScale = roomRenderer.transform.lossyScale;
                 float targetWorldScale = 0.05f; // Default fallback scale
                 string spriteNameLower = memoSprite.name.ToLower();
                 if (spriteNameLower.Contains("squirrel") || spriteNameLower.Contains("mouse"))
                 {
                     targetWorldScale = 0.07f;
                 }
                 else if (spriteNameLower.Contains("roedeer"))
                 {
                     targetWorldScale = 0.03f;
                 }
                 else if (spriteNameLower.Contains("rabbit"))
                 {
                    targetWorldScale = 0.05f;
                 }
                memoObj.transform.localScale = new Vector3(
                    parentWorldScale.x != 0f ? (targetWorldScale / parentWorldScale.x) : targetWorldScale,
                    parentWorldScale.y != 0f ? (targetWorldScale / parentWorldScale.y) : targetWorldScale,
                    1f
                );

                // Calculate world position to align with top-right corner, shifted inwards
                Vector3 worldTopRight = roomRenderer.bounds.max;
                Vector3 worldSize = roomRenderer.bounds.size;

                // Shift left by 15% and down by 25% of the room's world size (to go further down inside)
                worldTopRight.x -= worldSize.x * 0.15f;
                worldTopRight.y -= worldSize.y * 0.3f; 
                worldTopRight.z = roomRenderer.transform.position.z - 0.1f; // Place in front

                memoObj.transform.position = worldTopRight;

                Debug.Log($"[RoomUI] Assigned sprite {memoSprite.name} to {roomRenderer.gameObject.name}.");
            }
        }
    }
}
