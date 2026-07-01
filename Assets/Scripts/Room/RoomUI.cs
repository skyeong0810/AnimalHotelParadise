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
        [SerializeField] private Color assignButtonActiveColor = Color.white;
        [SerializeField] private Color assignButtonInactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

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
            if (roomManager == null || counterFlow == null) return;

            var guest = counterFlow.GetCurrentGuest();
            if (guest == null)
            {
                Debug.LogWarning("[RoomUI] No current guest to assign.");
                return;
            }
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected.");
                return;
            }

            bool success = roomManager.AssignRoom(_selectedRoomNumber, guest);
            if (success)
            {
                RefreshRoomGrid();
                OnRoomAssigned?.Invoke();
                if (dialogueManager != null) dialogueManager.NotifyRoomAssigned();
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

                if (i + 1 == _selectedRoomNumber)
                    sr.sprite = selectedSprite;
                else if (room.status == RoomStatus.Occupied)
                    sr.sprite = occupiedPlaceholderSprite;
                else
                    sr.sprite = vacantSprite;
            }
            RefreshAssignButton();
        }

        private void RefreshAssignButton()
        {
            if (assignButtonRenderer == null) return;
            bool canAssign = _selectedRoomNumber != -1
                && counterFlow != null
                && counterFlow.GetCurrentGuest() != null
                && roomManager.GetRoom(_selectedRoomNumber).status == RoomStatus.Vacant;

            assignButtonRenderer.color = canAssign
                ? assignButtonActiveColor
                : assignButtonInactiveColor;
        }
    }
}