using System.Collections.Generic;
using UnityEngine;
using TMPro;

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

        [Header("memo_sfx")]
        [SerializeField] private AudioSource memoSfxSource;
        [SerializeField] private AudioClip memoSfx;
        [Range(0f, 1f)]
        [SerializeField] private float memoSfxVolume = 1f;

        [Header("room_sfx")]
        [SerializeField] private AudioClip roomSelectSfx;
        [SerializeField] private AudioClip roomCleanSfx;
        [SerializeField] private AudioClip advancedCleanSfx;
        [Range(0f, 1f)]
        [SerializeField] private float roomSelectSfxVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float roomCleanSfxVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float advancedCleanSfxVolume = 1f;

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

        [Header("menu_labels")]
        [SerializeField] private TMP_FontAsset menuButtonLabelFont;
        [SerializeField] private Color menuButtonLabelColor = new Color(0.12f, 0.08f, 0.07f, 1f);
        [SerializeField] private float menuButtonLabelFontSize = 0.55f;
        [SerializeField] private float menuButtonAdvancedLabelFontSize = 0.42f;
        [SerializeField] private int menuButtonLabelSortingOffset = 20;

        [SerializeField] private Color assignButtonActiveColor = Color.white;
        [SerializeField] private Color assignButtonInactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("room_status_colors")]
        [SerializeField] private Color vacantRoomColor = Color.clear;
        [SerializeField] private Color selectedRoomColor = Color.red;
        [SerializeField] private Color occupiedRoomColor = Color.clear;

        [Header("room_status_icons")]
        [SerializeField] private Sprite cleaningStatusSprite;
        [SerializeField] private Sprite advancedCleaningStatusSprite;
        [Min(0.01f)]
        [SerializeField] private float roomStatusIconWorldSize = 0.65f;
        [SerializeField] private Vector2 roomStatusIconLocalOffset = Vector2.zero;
        [SerializeField] private int roomStatusIconSortingOffset = 8;

        public event System.Action OnRoomAssigned;

        private int _selectedRoomNumber = -1;

        private void Awake()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
            if (memoSfxSource == null && counterFlow != null) memoSfxSource = counterFlow.GetComponent<AudioSource>();
            InitializeRoomRenderers();
            EnsureMenuButtonLabels();
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
            EnsureMenuButtonLabels();
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
                ClearRoomMemos(_selectedRoomNumber);
                _selectedRoomNumber = -1;
                RefreshRoomGrid();
                OnRoomAssigned?.Invoke();
                if (dialogueManager != null) dialogueManager.NotifyRoomAssigned();
            }
        }

        public void OnCleanButtonClicked()
        {
            PlayRoomSfx(roomCleanSfx, roomCleanSfxVolume);
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
            PlayRoomSfx(advancedCleanSfx, advancedCleanSfxVolume);
            if (roomManager == null) return;
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected.");
                return;
            }

            roomManager.AdvancedCleanRoom(_selectedRoomNumber);
            RefreshRoomGrid();
        }

        public void OnMemoDeleteButtonClicked()
        {
            if (_selectedRoomNumber == -1)
            {
                Debug.LogWarning("[RoomUI] No room selected to delete memos.");
                return;
            }

            if (ClearRoomMemos(_selectedRoomNumber))
            {
                PlayMemoSfx();
                Debug.Log($"[RoomUI] Deleted all memos in room {_selectedRoomNumber}.");
            }
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
                sr.color = GetRoomColor(room, i + 1 == _selectedRoomNumber);
                RefreshRoomCleaningIcon(sr, room);

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

        private void EnsureMenuButtonLabels()
        {
            EnsureMenuButtonLabel(assignButtonRenderer, "배치", menuButtonLabelFontSize);
            EnsureMenuButtonLabel(cleanButtonRenderer, "청소", menuButtonLabelFontSize);
            EnsureMenuButtonLabel(advancedCleanButtonRenderer, "고급 청소", menuButtonAdvancedLabelFontSize);

            var memoRenderer = FindMemoButtonRenderer();
            EnsureMenuButtonLabel(memoRenderer, "메모", menuButtonLabelFontSize);
        }

        private SpriteRenderer FindMemoButtonRenderer()
        {
            Transform memoTransform = transform.Find("RoomBackground/MenuBackground/Memo");
            return memoTransform != null ? memoTransform.GetComponent<SpriteRenderer>() : null;
        }

        private void EnsureMenuButtonLabel(SpriteRenderer buttonRenderer, string label, float fontSize)
        {
            if (buttonRenderer == null) return;

            Transform labelTransform = buttonRenderer.transform.Find("ButtonLabel");
            TextMeshPro labelText;
            if (labelTransform == null)
            {
                GameObject labelObj = new GameObject("ButtonLabel");
                labelObj.transform.SetParent(buttonRenderer.transform, false);
                labelText = labelObj.AddComponent<TextMeshPro>();
                labelTransform = labelText.transform;
            }
            else
            {
                labelText = labelTransform.GetComponent<TextMeshPro>();
                if (labelText == null)
                {
                    labelText = labelTransform.gameObject.AddComponent<TextMeshPro>();
                    labelTransform = labelText.transform;
                }
            }


            labelTransform.localPosition = new Vector3(0f, 0f, -0.05f);
            labelTransform.localRotation = Quaternion.identity;

            Vector3 parentScale = buttonRenderer.transform.lossyScale;
            float xScale = Mathf.Abs(parentScale.x) > 0.0001f ? 1f / Mathf.Abs(parentScale.x) : 1f;
            float yScale = Mathf.Abs(parentScale.y) > 0.0001f ? 1f / Mathf.Abs(parentScale.y) : 1f;
            labelTransform.localScale = new Vector3(xScale, yScale, 1f);

            if (menuButtonLabelFont != null) labelText.font = menuButtonLabelFont;
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.color = menuButtonLabelColor;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.enableWordWrapping = false;
            labelText.overflowMode = TextOverflowModes.Overflow;
            labelText.sortingLayerID = buttonRenderer.sortingLayerID;
            labelText.sortingOrder = buttonRenderer.sortingOrder + menuButtonLabelSortingOffset;
            labelText.rectTransform.sizeDelta = new Vector2(1.6f, 0.6f);

            var meshRenderer = labelText.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingLayerID = buttonRenderer.sortingLayerID;
                meshRenderer.sortingOrder = labelText.sortingOrder;
            }

            labelText.ForceMeshUpdate();
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

        private Color GetRoomColor(RoomData room, bool isSelected)
        {
            if (isSelected) return selectedRoomColor;
            if (room == null) return vacantRoomColor;

            switch (room.status)
            {
                case RoomStatus.Occupied:
                    return occupiedRoomColor;
                case RoomStatus.NeedsExamination:
                case RoomStatus.NeedsCleaning:
                case RoomStatus.AdvancedCleaningInProgress:
                    return vacantRoomColor;
                default:
                    return vacantRoomColor;
            }
        }

        private bool IsMaintenanceRoom(RoomData room)
        {
            return room != null
                && (room.status == RoomStatus.NeedsExamination || room.status == RoomStatus.NeedsCleaning);
        }

        private int GetMemoSlotIndex(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return -1;
            string nameLower = spriteName.ToLower();
            if (nameLower.Contains("squirrel")) return 0;
            if (nameLower.Contains("roe_deer")) return 1;
            if (nameLower.Contains("mouse")) return 2;
            if (nameLower.Contains("rabbit")) return 3;
            return -1;
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

            if (memoSprite == null)
            {
                Debug.LogWarning("[RoomUI] Memo sprite is null.");
                return;
            }

            int slotIndex = GetMemoSlotIndex(memoSprite.name);
            if (slotIndex == -1)
            {
                Debug.LogWarning($"[RoomUI] Unknown memo sprite species: {memoSprite.name}");
                return;
            }

            // Clean up old single memo format if it exists to avoid visual clutter
            Transform oldMemoTransform = roomRenderer.transform.Find("RoomMemoSprite");
            if (oldMemoTransform != null)
            {
                if (Application.isPlaying) Destroy(oldMemoTransform.gameObject);
                else DestroyImmediate(oldMemoTransform.gameObject);
            }

            string childName = $"RoomMemoSprite_{slotIndex}";
            Transform memoTransform = roomRenderer.transform.Find(childName);

            if (memoTransform != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(memoTransform.gameObject);
                }
                else
                {
                    DestroyImmediate(memoTransform.gameObject);
                }
                Debug.Log($"[RoomUI] Deleted memo sprite {memoSprite.name} from room {_selectedRoomNumber} slot {slotIndex}.");
                PlayMemoSfx();
                return;
            }

            GameObject memoObj = new GameObject(childName);
            memoObj.transform.SetParent(roomRenderer.transform, false);
            SpriteRenderer memoSr = memoObj.AddComponent<SpriteRenderer>();

            if (memoSr != null)
            {
                memoSr.sprite = memoSprite;
                memoSr.sortingLayerID = roomRenderer.sortingLayerID;
                memoSr.sortingLayerName = roomRenderer.sortingLayerName;
                memoSr.sortingOrder = roomRenderer.sortingOrder + 10;
                
                // Prevent stretching by neutralizing the parent's lossy (world) scale.
                Vector3 parentWorldScale = roomRenderer.transform.lossyScale;
                float targetWorldScale = 0.05f; // Default fallback scale
                string spriteNameLower = memoSprite.name.ToLower();
                if (spriteNameLower.Contains("squirrel") || spriteNameLower.Contains("mouse"))
                {
                    targetWorldScale = 0.06f;
                }
                else if (spriteNameLower.Contains("roedeer"))
                {
                    targetWorldScale = 0.01f;
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

                // Calculate world position based on slotIndex: squirrel, roedeer, mouse, rabbit (0, 1, 2, 3)
                // Using world bounds of the roomRenderer ensures correct positioning regardless of custom pivots or sprite dimensions.
                float roomMinX = roomRenderer.bounds.min.x;
                float roomWidth = roomRenderer.bounds.size.x;
                
                // Distribute across 4 slots: 0.15f, 0.40f, 0.65f, 0.90f of room width
                float slotX = roomMinX + roomWidth * (0.17f + slotIndex * 0.23f);
                float slotY = roomRenderer.bounds.max.y - roomRenderer.bounds.size.y * 0.15f;
                float slotZ = roomRenderer.transform.position.z - 0.1f; // Place in front

                Vector3 worldSlotPos = new Vector3(slotX, slotY, slotZ);
                // Convert world position to parent's local space so that the memo moves dynamically with the room's transform.
                memoObj.transform.localPosition = roomRenderer.transform.InverseTransformPoint(worldSlotPos);

            }

            PlayMemoSfx();
        }

        private void PlayRoomSfx(AudioClip clip, float volume)
        {
            if (memoSfxSource == null && counterFlow != null)
            {
                memoSfxSource = counterFlow.GetComponent<AudioSource>();
            }

            if (memoSfxSource != null && clip != null)
            {
                memoSfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            }
        }

        private void PlayMemoSfx()
        {
            if (memoSfxSource == null && counterFlow != null)
            {
                memoSfxSource = counterFlow.GetComponent<AudioSource>();
            }

            if (memoSfxSource != null && memoSfx != null)
            {
                memoSfxSource.PlayOneShot(memoSfx, Mathf.Clamp01(memoSfxVolume));
            }
        }

        private bool ClearRoomMemos(int roomNumber)
        {
            if (roomRenderers == null || roomNumber < 1 || roomNumber > roomRenderers.Count) return false;
            var roomRenderer = roomRenderers[roomNumber - 1];
            if (roomRenderer == null) return false;

            bool removedAny = false;
            for (int i = roomRenderer.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = roomRenderer.transform.GetChild(i);
                if (child != null && (child.name.StartsWith("RoomMemoSprite_") || child.name == "RoomMemoSprite"))
                {
                    removedAny = true;
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            return removedAny;
        }

        private void RefreshRoomCleaningIcon(SpriteRenderer roomRenderer, RoomData room)
        {
            if (roomRenderer == null) return;

            const string childName = "RoomCleaningStatusIcon";
            Transform iconTransform = roomRenderer.transform.Find(childName);
            Sprite iconSprite = GetRoomCleaningIconSprite(room);

            if (iconSprite == null)
            {
                if (iconTransform != null)
                    iconTransform.gameObject.SetActive(false);
                return;
            }

            GameObject iconObject;
            SpriteRenderer iconRenderer;
            if (iconTransform == null)
            {
                iconObject = new GameObject(childName);
                iconObject.transform.SetParent(roomRenderer.transform, false);
                iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            }
            else
            {
                iconObject = iconTransform.gameObject;
                iconRenderer = iconObject.GetComponent<SpriteRenderer>();
                if (iconRenderer == null)
                    iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            }

            iconObject.SetActive(true);
            iconRenderer.sprite = iconSprite;
            iconRenderer.color = Color.white;
            iconRenderer.sortingLayerID = roomRenderer.sortingLayerID;
            iconRenderer.sortingLayerName = roomRenderer.sortingLayerName;
            iconRenderer.sortingOrder = roomRenderer.sortingOrder + roomStatusIconSortingOffset;

            Vector2 spriteSize = iconSprite.bounds.size;
            float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
            float uniformWorldScale = largestSide > 0.0001f
                ? Mathf.Max(0.01f, roomStatusIconWorldSize) / largestSide
                : 1f;

            Vector3 parentWorldScale = roomRenderer.transform.lossyScale;
            float parentScaleX = Mathf.Abs(parentWorldScale.x) > 0.0001f ? Mathf.Abs(parentWorldScale.x) : 1f;
            float parentScaleY = Mathf.Abs(parentWorldScale.y) > 0.0001f ? Mathf.Abs(parentWorldScale.y) : 1f;
            iconObject.transform.localScale = new Vector3(
                uniformWorldScale / parentScaleX,
                uniformWorldScale / parentScaleY,
                1f
            );
            iconObject.transform.localRotation = Quaternion.identity;

            Vector3 desiredCenter = new Vector3(
                roomStatusIconLocalOffset.x,
                roomStatusIconLocalOffset.y,
                -0.1f
            );
            Vector3 spriteCenterOffset = Vector3.Scale(iconSprite.bounds.center, iconObject.transform.localScale);
            iconObject.transform.localPosition = desiredCenter - spriteCenterOffset;
        }

        private Sprite GetRoomCleaningIconSprite(RoomData room)
        {
            if (room == null) return null;

            switch (room.status)
            {
                case RoomStatus.NeedsExamination:
                    return cleaningStatusSprite;
                case RoomStatus.NeedsCleaning:
                case RoomStatus.AdvancedCleaningInProgress:
                    return advancedCleaningStatusSprite;
                default:
                    return null;
            }
        }
    }
}
