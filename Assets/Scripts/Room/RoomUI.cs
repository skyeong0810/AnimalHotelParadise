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