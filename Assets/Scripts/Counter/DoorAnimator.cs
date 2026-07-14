using System.Collections;
using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 손님 입장용 문. 하나의 SpriteRenderer가 닫힘/열림 프레임을 바꾸며 재생된다.
    /// </summary>
    public class DoorAnimator : MonoBehaviour
    {
        [Header("Door Sprite")]
        [SerializeField] private SpriteRenderer doorRenderer;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openingSprite;
        [SerializeField] private Sprite openSprite;

        [Header("Timing")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.35f;

        private void Awake()
        {
            EnsureRenderer();
            Show(closedSprite);
        }

        public IEnumerator Open()
        {
            yield return Play(closedSprite, openingSprite, openSprite, openDuration);
        }

        public IEnumerator Close()
        {
            yield return Play(openSprite, openingSprite, closedSprite, closeDuration);
        }

        private IEnumerator Play(Sprite firstFrame, Sprite middleFrame, Sprite lastFrame, float duration)
        {
            EnsureRenderer();
            if (doorRenderer == null) yield break;

            Sprite[] frames = { firstFrame, middleFrame, lastFrame };
            float frameDelay = duration > 0f ? duration / Mathf.Max(1, frames.Length - 1) : 0f;

            for (int i = 0; i < frames.Length; i++)
            {
                Show(frames[i]);

                if (i < frames.Length - 1 && frameDelay > 0f)
                {
                    yield return new WaitForSeconds(frameDelay);
                }
            }
        }

        private void EnsureRenderer()
        {
            if (doorRenderer == null)
            {
                doorRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void Show(Sprite sprite)
        {
            if (doorRenderer != null && sprite != null)
            {
                doorRenderer.sprite = sprite;
            }
        }
    }
}

