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

        [Header("Door Sfx")]
        [SerializeField] private AudioSource doorSfxSource;
        [SerializeField] private AudioClip openSfx;
        [Range(0f, 1f)]
        [SerializeField] private float openSfxVolume = 1f;
        [Tooltip("음수는 애니메이션보다 먼저, 양수는 애니메이션 시작 후에 재생됩니다.")]
        [SerializeField] private float openSfxTimingOffsetSeconds = 0f;

        [Header("Timing")]
        [SerializeField] private float openDuration = 0.35f;
        [SerializeField] private float closeDuration = 0.35f;
        [Header("Mirrored Open Frame")]
        [SerializeField] private float mirroredOpenOffsetX = -2.38f;
        [SerializeField] private float mirroredOpeningOffsetX = -1.09f;

        private Vector3 doorBaseLocalPosition;

        private void Awake()
        {
            EnsureRenderer();
            if (doorSfxSource == null) doorSfxSource = GetComponent<AudioSource>();
            if (doorRenderer != null)
            {
                doorBaseLocalPosition = doorRenderer.transform.localPosition;
            }
            Show(closedSprite, false, 0f);
        }

        public IEnumerator Open()
        {
            if (openSfxTimingOffsetSeconds < 0f)
            {
                PlayOpenSfx();
                yield return new WaitForSeconds(-openSfxTimingOffsetSeconds);
            }
            else if (openSfxTimingOffsetSeconds > 0f)
            {
                StartCoroutine(PlayOpenSfxAfterDelay(openSfxTimingOffsetSeconds));
            }
            else
            {
                PlayOpenSfx();
            }

            Sprite[] frames = { closedSprite, openingSprite, openSprite, openSprite, openingSprite };
            bool[] flipXStates = { false, false, false, true, true };
            float[] offsetXStates = { 0f, 0f, 0f, mirroredOpenOffsetX, mirroredOpeningOffsetX };
            yield return Play(frames, flipXStates, offsetXStates, openDuration);
        }

        public IEnumerator Close()
        {
            Sprite[] frames = { openingSprite, openSprite, openSprite, openingSprite, closedSprite };
            bool[] flipXStates = { true, true, false, false, false };
            float[] offsetXStates = { mirroredOpeningOffsetX, mirroredOpenOffsetX, 0f, 0f, 0f };
            yield return Play(frames, flipXStates, offsetXStates, closeDuration);
        }

        private IEnumerator Play(Sprite[] frames, bool[] flipXStates, float[] offsetXStates, float duration)
        {
            EnsureRenderer();
            if (doorRenderer == null || frames == null || frames.Length == 0) yield break;

            float frameDelay = duration > 0f ? duration / Mathf.Max(1, frames.Length - 1) : 0f;

            for (int i = 0; i < frames.Length; i++)
            {
                bool flipX = flipXStates != null && i < flipXStates.Length && flipXStates[i];
                float offsetX = offsetXStates != null && i < offsetXStates.Length
                    ? offsetXStates[i]
                    : 0f;
                Show(frames[i], flipX, offsetX);

                if (i < frames.Length - 1 && frameDelay > 0f)
                {
                    yield return new WaitForSeconds(frameDelay);
                }
            }
        }

        private IEnumerator PlayOpenSfxAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            PlayOpenSfx();
        }

        private void PlayOpenSfx()
        {
            if (doorSfxSource == null) doorSfxSource = GetComponent<AudioSource>();
            if (doorSfxSource != null && openSfx != null)
            {
                doorSfxSource.PlayOneShot(openSfx, Mathf.Clamp01(openSfxVolume));
            }
        }

        private void EnsureRenderer()
        {
            if (doorRenderer == null)
            {
                doorRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void Show(Sprite sprite, bool flipX, float offsetX)
        {
            if (doorRenderer != null && sprite != null)
            {
                doorRenderer.sprite = sprite;
                doorRenderer.flipX = flipX;
                doorRenderer.transform.localPosition = doorBaseLocalPosition +
                    new Vector3(offsetX, 0f, 0f);
            }
        }
    }
}

