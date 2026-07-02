using System.Collections;
using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 자동문 스타일 입장 문.
    /// 두 패널(왼쪽/오른쪽)이 좌우로 미끄러지며 열림/닫힘.
    /// </summary>
    public class DoorAnimator : MonoBehaviour
    {
        [Header("Door Panels")]
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;

        [Header("Settings")]
        [SerializeField] private float openOffsetX  = 0.9f;
        [SerializeField] private float openDuration  = 0.35f;
        [SerializeField] private float closeDuration = 0.35f;

        private Vector3 _leftClosedPos, _leftOpenPos;
        private Vector3 _rightClosedPos, _rightOpenPos;
        private bool _initialized;

        private void Awake() => CacheInitial();

        private void CacheInitial()
        {
            if (_initialized) return;
            if (leftPanel == null || rightPanel == null) return;

            _leftClosedPos  = leftPanel.position;
            _rightClosedPos = rightPanel.position;
            _leftOpenPos    = _leftClosedPos  + Vector3.left  * openOffsetX;
            _rightOpenPos   = _rightClosedPos + Vector3.right * openOffsetX;
            _initialized = true;
        }

        public IEnumerator Open()
        {
            CacheInitial();
            yield return MoveBoth(_leftOpenPos, _rightOpenPos, openDuration);
        }

        public IEnumerator Close()
        {
            CacheInitial();
            yield return MoveBoth(_leftClosedPos, _rightClosedPos, closeDuration);
        }

        private IEnumerator MoveBoth(Vector3 leftTarget, Vector3 rightTarget, float duration)
        {
            Vector3 leftStart  = leftPanel.position;
            Vector3 rightStart = rightPanel.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                leftPanel.position  = Vector3.Lerp(leftStart,  leftTarget,  k);
                rightPanel.position = Vector3.Lerp(rightStart, rightTarget, k);
                yield return null;
            }
            leftPanel.position  = leftTarget;
            rightPanel.position = rightTarget;
        }
    }
}
