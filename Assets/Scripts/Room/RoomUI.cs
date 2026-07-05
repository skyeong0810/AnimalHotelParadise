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

        [Header("reservation_list")]
        [SerializeField] private Transform background;
        [SerializeField] private float spacing = 1.2f;
        [SerializeField] private float scale = 1.0f;

        [Header("room_buttons")]
        [SerializeField] private List<SpriteRenderer> roomRenderers;
        [SerializeField] private Sprite vacantSprite;
        [SerializeField] private Sprite occupiedPlaceholderSprite;
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


        public event System.Action OnRoomAssigned;

        private int _selectedRoomNumber = -1;

        private void Awake()
        {
            InitializeRoomRenderers();
        }

        private void InitializeRoomRenderers()
        {
            if (roomRenderers == null || roomRenderers.Count == 0)
            {
                var buttons = FindObjectsOfType<RoomButton>();
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

            roomManager.CleanRoom(_selectedRoomNumber);
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
                dayManager = FindObjectOfType<DayManager>();
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

            // Convert target world scale, spacing, and margin to local X units
            float localIconWidth = parentScaleX != 0f ? (scale / parentScaleX) : scale;
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

            // First icon (i = 0) is at the right edge
            float startLocalX = localRightEdge - (0.5f * localIconWidth) - localMargin;

            for (int i = 0; i < numIcons; i++)
            {
                var guest = reservationGuests[i];
                if (guest.species == null || guest.species.speciesSprite == null) continue;

                GameObject iconObj = new GameObject("ReservationIcon_" + guest.guestName);
                iconObj.transform.SetParent(background);

                // Position: start from right (startLocalX) and move left (- i * actualLocalSpacing)
                float posX = startLocalX - (i * actualLocalSpacing);

                iconObj.transform.localPosition = new Vector3(posX, 0f, 0f);

                // Calculate local scale
                float localX = parentScaleX != 0f ? (scale / parentScaleX) : scale;
                float localY = backgroundLossyScale.y != 0f ? (scale / backgroundLossyScale.y) : scale;
                float localZ = backgroundLossyScale.z != 0f ? (scale / backgroundLossyScale.z) : scale;
                iconObj.transform.localScale = new Vector3(localX, localY, localZ);
                iconObj.transform.localRotation = Quaternion.identity;

                SpriteRenderer sr = iconObj.AddComponent<SpriteRenderer>();
                sr.sprite = guest.species.speciesSprite;
                sr.sortingOrder = 60; // Make sure it's visible on top of panels
            }
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
            if (room != null && room.status == RoomStatus.Occupied && occupiedPlaceholderSprite != null) return occupiedPlaceholderSprite;
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
                default:
                    return vacantRoomColor;
            }
        }

        private bool IsMaintenanceRoom(RoomData room)
        {
            return room != null
                && (room.status == RoomStatus.NeedsExamination || room.status == RoomStatus.NeedsCleaning);
        }



    }
}