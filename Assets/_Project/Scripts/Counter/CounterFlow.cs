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

        [Header("Dialogue System")]
        [SerializeField] private DialogueManager dialogueManager;

        [Header("Data")]
        [SerializeField] private DayManager dayManager;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip doorBellSfx;
        [SerializeField] private AudioClip footstepSfx;
        [SerializeField] private AudioClip exitBellSfx;

        [Header("Timing")]
        [SerializeField] private float delayBeforeStart = 0.5f;
        [SerializeField] private float delayAfterDoorOpen = 0.15f;
        [SerializeField] private float delayBeforeDialogue = 0.35f;
        [SerializeField] private float delayAfterResponse = 0.4f;
        [SerializeField] private float delayBetweenCustomers = 1.0f;
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private bool autoSpawnNextCustomer = true;

        private bool _isSpawning;
        private int _guestIndex;
        private bool _dialogueFinished;
        private string _exitNodeId;

        private void Start()
        {
            if (dialogueManager != null) dialogueManager.OnDialogueEnd += OnDialogueEnd;
            if (autoStartOnPlay) StartCoroutine(DelayedStart());
        }

        private void OnDestroy()
        {
            if (dialogueManager != null) dialogueManager.OnDialogueEnd -= OnDialogueEnd;
        }

        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(delayBeforeStart);
            yield return SpawnCustomerRoutine();
        }

        public IEnumerator SpawnCustomerRoutine()
        {
            if (_isSpawning) yield break;
            _isSpawning = true;
            if (customerBubble != null) customerBubble.HideImmediate();

            Animal guest = GetNextGuest();
            if (guest == null)
            {
                Debug.Log("[CounterFlow] 오늘의 손님이 모두 방문했습니다.");
                _isSpawning = false;
                yield break;
            }

            Debug.Log(string.Format("[CounterFlow] 손님 등장: {0} ({1}) 예약:{2}", guest.guestName, guest.species.displayName, guest.hasReservation));

            PlaySfx(doorBellSfx);
            if (door != null) yield return door.Open();
            if (delayAfterDoorOpen > 0f) yield return new WaitForSeconds(delayAfterDoorOpen);
            PlaySfx(footstepSfx);
            if (customerSlot != null) yield return customerSlot.Spawn();
            if (door != null) yield return door.Close();

            if (delayBeforeDialogue > 0f) yield return new WaitForSeconds(delayBeforeDialogue);
            if (dialogueManager != null)
            {
                _dialogueFinished = false;
                _exitNodeId = null;
                bool claimsReservation = guest.hasReservation;
                dialogueManager.StartDialogue(guest, claimsReservation);
                yield return new WaitUntil(() => _dialogueFinished);
                Debug.Log("[CounterFlow] 대화 종료: " + _exitNodeId);
            }

            if (delayAfterResponse > 0f) yield return new WaitForSeconds(delayAfterResponse);
            if (customerBubble != null) customerBubble.HideImmediate();
            PlaySfx(exitBellSfx);
            if (customerSlot != null) yield return customerSlot.Sink();
            _isSpawning = false;

            if (autoSpawnNextCustomer && Application.isPlaying)
            {
                yield return new WaitForSeconds(delayBetweenCustomers);
                StartCoroutine(SpawnCustomerRoutine());
            }
        }

        private Animal GetNextGuest()
        {
            if (dayManager == null) { Debug.LogWarning("[CounterFlow] DayManager 미연결"); return null; }
            if (dayManager.TodaysGuests == null || _guestIndex >= dayManager.TodaysGuests.Count) return null;
            var guest = dayManager.TodaysGuests[_guestIndex];
            _guestIndex++;
            dayManager.GuestArrived(guest.guestName);
            return guest;
        }

        private void OnDialogueEnd(string exitNodeId) { _exitNodeId = exitNodeId; _dialogueFinished = true; }
        private void PlaySfx(AudioClip clip) { if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip); }
    }
}
