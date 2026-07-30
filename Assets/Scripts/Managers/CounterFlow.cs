using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalHotel.Counter
{
    public class CounterFlow : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private DoorAnimator door;
        [SerializeField] private SimpleCustomerSlot customerSlot;
        [SerializeField] private SpeechBubble customerBubble;
        [SerializeField] private RoomAssignmentKeyAnimator roomAssignmentKey;

        [Header("Dialogue System")]
        [SerializeField] private DialogueManager dialogueManager;

        [Header("Data")]
        [SerializeField] private DayManager dayManager;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip doorBellSfx;
        [SerializeField] private AudioClip footstepSfx;
        [SerializeField] private AudioClip entryWhooshSfx;
        [SerializeField] private AudioClip rabbitFootstepSfx;
        [SerializeField] private AudioClip roeDeerFootstepSfx;
        [SerializeField] private AudioClip exitBellSfx;

        [Header("Audio Volumes")]
        [Range(0f, 1f)][SerializeField] private float masterSfxVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float doorBellVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float footstepVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float exitBellVolume = 1f;

        [Header("Timing")]
        [SerializeField] private float delayBeforeStart = 0.5f;
        [SerializeField] private float delayAfterDoorOpen = 0.15f;
        [SerializeField] private float delayBeforeDialogue = 0.35f;
        [SerializeField] private float delayAfterResponse = 0.4f;
        [SerializeField] private float delayBetweenCustomers = 1.0f;
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private bool autoSpawnNextCustomer = true;

        [Header("Checkout Timing")]
        [SerializeField] private float checkoutDelayBeforeKey = 0.25f;
        [SerializeField] private float checkoutKeyHoldDuration = 0.45f;
        [SerializeField] private float checkoutDelayAfterDoorOpen = 0.15f;
        [SerializeField] private float checkoutDelayBetweenGuests = 0.5f;

        public bool _isSpawning;
        private int _guestIndex;
        private bool _dialogueFinished;
        private string _exitNodeId;
        private Animal _currentGuest;

        public bool IsBusy => _isSpawning;
        public Animal GetCurrentGuest() => _currentGuest;

        private void Start()
        {
            if (roomAssignmentKey == null) roomAssignmentKey = FindFirstObjectByType<RoomAssignmentKeyAnimator>();
            if (dialogueManager != null) dialogueManager.OnDialogueEnd += OnDialogueEnd;
            if (dialogueManager != null) dialogueManager.OnChoiceResolved += OnChoiceResolved;
            if (autoStartOnPlay) StartCoroutine(DelayedStart());
        }

        private void OnDestroy()
        {
            if (dialogueManager != null) dialogueManager.OnDialogueEnd -= OnDialogueEnd;
            if (dialogueManager != null) dialogueManager.OnChoiceResolved -= OnChoiceResolved;
        }

        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(delayBeforeStart);
            yield return SpawnCustomerRoutine();
        }

        public IEnumerator SpawnCustomerRoutine()
        {
            if (_isSpawning || (dayManager != null && dayManager.IsCheckoutInProgress)) yield break;
            _isSpawning = true;
            if (customerBubble != null) customerBubble.HideImmediate();
            if (roomAssignmentKey != null) roomAssignmentKey.HideImmediate();

            _currentGuest = GetNextGuest();
            if (_currentGuest == null)
            {
                Debug.Log("[CounterFlow] 현재 시간대의 손님이 모두 방문했습니다.");
                _isSpawning = false;
                dayManager.TryAdvancePhase();
                yield break;
            }

            Debug.Log(string.Format("[CounterFlow] 손님 등장: {0} ({1}) 예약:{2}",
                _currentGuest.guestName, _currentGuest.species.displayName, _currentGuest.hasReservation));

            PlaySfx(doorBellSfx, doorBellVolume);
            if (door != null) yield return door.Open();
            if (delayAfterDoorOpen > 0f) yield return new WaitForSeconds(delayAfterDoorOpen);
            AudioClip entryFootstep = GetEntryFootstepSfx(_currentGuest);
            PlaySfx(entryFootstep, footstepVolume);
            if (entryFootstep != null) yield return new WaitForSeconds(entryFootstep.length);

            PlaySfx(GetEntryWhooshSfx(), footstepVolume);
            if (customerSlot != null) yield return customerSlot.Spawn(_currentGuest.species?.speciesSprite, _currentGuest.CounterSpriteScaleMultiplier);
            if (door != null) yield return door.Close();

            if (delayBeforeDialogue > 0f) yield return new WaitForSeconds(delayBeforeDialogue);
            if (dialogueManager != null)
            {
                _dialogueFinished = false;
                _exitNodeId = null;
                dialogueManager.StartDialogue(_currentGuest, _currentGuest.hasReservation);
                yield return new WaitUntil(() => _dialogueFinished);
                Debug.Log("[CounterFlow] 대화 종료: " + _exitNodeId);

                // Confirm check-in only when the dialogue result indicates the guest was accepted.
                // Adjust the exitNodeId string to match whatever your DialogueManager emits.
                if (_exitNodeId == "exit_checkin" || _exitNodeId == "exit_checkin_angry")
                    dayManager.CheckIn(_currentGuest);
                else if (_exitNodeId == "exit_rejected_no_room")
                    dayManager.RecordRating(_currentGuest, 0, "Reservation rejected because no room was available");
            }

            if (delayAfterResponse > 0f) yield return new WaitForSeconds(delayAfterResponse);
            if (customerBubble != null) customerBubble.HideImmediate();
            PlaySfx(exitBellSfx, exitBellVolume);
            if (customerSlot != null) yield return customerSlot.Sink();
            _currentGuest = null;
            _isSpawning = false;

            if (autoSpawnNextCustomer && Application.isPlaying)
            {
                yield return new WaitForSeconds(delayBetweenCustomers);
                StartCoroutine(SpawnCustomerRoutine());
            }
        }

        /// <summary>
        /// 체크아웃 대상 동물들을 한 마리씩 오른쪽에서 입장시키고,
        /// 카드키 반납 → 문 열림 → 동물 퇴장과 카드키 하강·페이드 아웃을 각각 재생 → 문 닫힘 순서로 재생한다.
        /// </summary>
        public IEnumerator PlayCheckoutSequence(IList<Animal> departingGuests, System.Action<Animal> onGuestCompleted)
        {
            if (_isSpawning)
            {
                Debug.LogWarning("[CounterFlow] 다른 카운터 연출이 진행 중이라 체크아웃을 시작하지 못했습니다.");
                yield break;
            }

            _isSpawning = true;
            if (customerBubble != null) customerBubble.HideImmediate();
            if (roomAssignmentKey != null) roomAssignmentKey.HideImmediate();

            if (departingGuests != null)
            {
                foreach (Animal guest in departingGuests)
                {
                    if (guest == null) continue;

                    _currentGuest = guest;
                    Debug.Log($"[CounterFlow] 체크아웃 손님 등장: {guest.guestName} ({guest.species?.displayName})");

                    if (customerSlot != null)
                        yield return customerSlot.EnterFromRight(guest.species?.speciesSprite, guest.CounterSpriteScaleMultiplier);

                    if (checkoutDelayBeforeKey > 0f)
                        yield return new WaitForSeconds(checkoutDelayBeforeKey);

                    if (roomAssignmentKey != null)
                        yield return roomAssignmentKey.ShowFromAbove();

                    if (checkoutKeyHoldDuration > 0f)
                        yield return new WaitForSeconds(checkoutKeyHoldDuration);

                    PlaySfx(exitBellSfx, exitBellVolume);
                    if (door != null)
                        yield return door.Open();

                    if (checkoutDelayAfterDoorOpen > 0f)
                        yield return new WaitForSeconds(checkoutDelayAfterDoorOpen);

                    if (roomAssignmentKey != null)
                        StartCoroutine(roomAssignmentKey.HideDownAndFade());

                    if (customerSlot != null)
                        yield return customerSlot.ExitThroughDoor(guest.species?.backSprite);

                    if (door != null)
                        yield return door.Close();

                    onGuestCompleted?.Invoke(guest);
                    _currentGuest = null;

                    if (checkoutDelayBetweenGuests > 0f)
                        yield return new WaitForSeconds(checkoutDelayBetweenGuests);
                }
            }

            if (roomAssignmentKey != null) roomAssignmentKey.HideImmediate();
            _currentGuest = null;
            _isSpawning = false;
        }


        /// <summary>
        /// Returns the next guest from the phase-appropriate arrival queue.
        /// Diurnals come from MorningArrivals; nocturnals from AfternoonArrivals.
        /// Calls GuestArrived so DayManager knows they've walked up to the counter.
        /// </summary>
        private Animal GetNextGuest()
        {
            if (dayManager == null) { Debug.LogWarning("[CounterFlow] DayManager 미연결"); return null; }

            // If the phase time has expired, no more new guests can arrive
            if (dayManager.PhaseTimeRemaining <= 0f) return null;

            var queue = dayManager.IsMorning ? dayManager.MorningArrivals : dayManager.AfternoonArrivals;
            if (queue == null || _guestIndex >= queue.Count) return null;

            var guest = queue[_guestIndex];
            _guestIndex++;
            dayManager.GuestArrived(guest.guestName);  // logs approach; no payment yet
            return guest;
        }

        /// <summary>
        /// Call this when the phase changes (morning ↔ afternoon) so the index resets
        /// to the start of the new queue. Wire this to DayManager's phase-change event
        /// or call it from your phase-transition UI code.
        /// </summary>
        public void OnPhaseChanged()
        {
            _guestIndex = 0;
            if (!_isSpawning && Application.isPlaying)
            {
                StartCoroutine(SpawnCustomerRoutine());
            }
        }

        private void OnDialogueEnd(string exitNodeId) { _exitNodeId = exitNodeId; _dialogueFinished = true; }
        private void OnChoiceResolved(string nextNodeId)
        {
            if (nextNodeId == "exit_checkin" || nextNodeId == "exit_checkin_angry")
                StartCoroutine(ShowRoomAssignmentKeyRoutine());
        }

        private IEnumerator ShowRoomAssignmentKeyRoutine()
        {
            if (roomAssignmentKey != null)
                yield return roomAssignmentKey.Show();
        }

        private void PlaySfx(AudioClip clip, float volume = 1f) { if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * masterSfxVolume)); }
    

        private AudioClip GetEntryFootstepSfx(Animal guest)
        {
            string speciesId = guest != null ? guest.SpeciesId : null;
            if (speciesId != null) speciesId = speciesId.ToLowerInvariant();
            switch (speciesId)
            {
                case "rabbit":
                    return rabbitFootstepSfx != null ? rabbitFootstepSfx : footstepSfx;
                case "roe_deer":
                    return roeDeerFootstepSfx != null ? roeDeerFootstepSfx : footstepSfx;
                default:
                    return footstepSfx;
            }
        }

        private AudioClip GetEntryWhooshSfx()
        {
            return entryWhooshSfx != null ? entryWhooshSfx : footstepSfx;
        }

    }
}
