using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// Counter 씬의 손님 응대 흐름.
    /// 종소리 → 문 열림 → 발소리 + 손님 솟아오름 → 문 닫힘 → 손님 말풍선 → 답변 옵션
    /// </summary>
    public class CounterFlow : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private DoorAnimator         door;
        [SerializeField] private SimpleCustomerSlot   customerSlot;
        [SerializeField] private SpeechBubble         customerBubble;
        [SerializeField] private SimpleResponseBubble responseBubble;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip   doorBellSfx;
        [Tooltip("손님이 카운터로 다가올 때 재생되는 발소리 (지금은 모든 손님 공통, 나중에 동물 데이터 별로 분리 예정)")]
        [SerializeField] private AudioClip   footstepSfx;

        [Header("Timing")]
        [SerializeField] private float delayBeforeStart       = 0.5f;
        [SerializeField] private float delayAfterDoorOpen     = 0.15f;
        [SerializeField] private float delayBeforeDialogue    = 0.35f;
        [SerializeField] private float delayBeforeOptions     = 0.3f;
        [SerializeField] private float delayAfterResponse     = 0.4f;
        [SerializeField] private float delayBetweenCustomers  = 1.0f;
        [SerializeField] private bool  autoStartOnPlay        = true;
        [SerializeField] private bool  autoSpawnNextCustomer  = true;

        [Header("Placeholder Data (나중에 동물/대사 데이터로 교체)")]
        [TextArea(1, 3)]
        [SerializeField] private string[] customerLines = new string[] { "", "", "" };
        [SerializeField] private List<string> responseOptions = new List<string> { "", "", "" };

        private int  _selectedIndex;
        private bool _gotResponse;
        private bool _isSpawning;

        private void Start()
        {
            if (autoStartOnPlay) StartCoroutine(DelayedStart());
        }

        private IEnumerator DelayedStart()
        {
            yield return new WaitForSeconds(delayBeforeStart);
            yield return SpawnCustomerRoutine();
        }

        [ContextMenu("▶ Spawn Customer (테스트)")]
        public void TriggerSpawn()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서만 테스트 가능합니다.");
                return;
            }
            StartCoroutine(SpawnCustomerRoutine());
        }

        public IEnumerator SpawnCustomerRoutine()
        {
            if (_isSpawning) yield break;
            _isSpawning = true;

            if (customerBubble != null) customerBubble.HideImmediate();
            if (responseBubble != null) responseBubble.HideImmediate();

            // 1. Doorbell + Door open
            PlaySfx(doorBellSfx);
            if (door != null) yield return door.Open();

            // 2. Footsteps + Customer rises
            if (delayAfterDoorOpen > 0f) yield return new WaitForSeconds(delayAfterDoorOpen);
            PlaySfx(footstepSfx);
            if (customerSlot != null) yield return customerSlot.Spawn();

            // 3. Door close
            if (door != null) yield return door.Close();

            // 4. Customer dialogue (box-only for now)
            if (customerBubble != null && customerLines != null && customerLines.Length > 0)
            {
                if (delayBeforeDialogue > 0f) yield return new WaitForSeconds(delayBeforeDialogue);
                string line = customerLines[Random.Range(0, customerLines.Length)];
                yield return customerBubble.ShowWithText(line);
            }

            // 5. Player response options
            if (responseBubble != null && responseOptions != null && responseOptions.Count > 0)
            {
                if (delayBeforeOptions > 0f) yield return new WaitForSeconds(delayBeforeOptions);
                _gotResponse = false;
                _selectedIndex = -1;
                responseBubble.OnOptionSelected += OnSelected;
                responseBubble.Show(responseOptions);
                yield return new WaitUntil(() => _gotResponse);
                responseBubble.OnOptionSelected -= OnSelected;
                Debug.Log("[CounterFlow] Selected option index: " + _selectedIndex);
            }

            // 6. Customer leaves
            if (delayAfterResponse > 0f) yield return new WaitForSeconds(delayAfterResponse);
            if (customerBubble != null) customerBubble.HideImmediate();
            if (customerSlot != null) yield return customerSlot.Sink();

            _isSpawning = false;

            // 7. Next customer
            if (autoSpawnNextCustomer && Application.isPlaying)
            {
                yield return new WaitForSeconds(delayBetweenCustomers);
                StartCoroutine(SpawnCustomerRoutine());
            }
        }

        private void OnSelected(int index)
        {
            _selectedIndex = index;
            _gotResponse = true;
        }

        private void PlaySfx(AudioClip clip)
        {
            if (sfxSource != null && clip != null)
                sfxSource.PlayOneShot(clip);
        }
    }
}
