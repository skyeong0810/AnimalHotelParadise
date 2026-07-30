using System.Collections;
using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 카운터 뒤 손님(동물) 자리.
    /// 숨김 위치(카운터 아래)에서 보이는 위치(카운터 위)로 솟아오름.
    /// 나중에 팀원이 만든 AnimalData가 들어오면 Spawn(AnimalData)로 확장 예정.
    /// </summary>
    public class SimpleCustomerSlot : MonoBehaviour
    {
        [Header("Y Positions")]
        [SerializeField] private float hiddenY  = -3.0f;
        [SerializeField] private float visibleY =  1.0f;

        [Header("Animation")]
        [SerializeField] private float riseDuration = 0.7f;
        [SerializeField] private float sinkDuration = 0.5f;

        [Header("Checkout Animation")]
        [Tooltip("How far right of the counter the animal starts during checkout.")]
        [SerializeField] private float checkoutStartOffsetX = 6f;
        [SerializeField] private float checkoutEnterDuration = 0.65f;

        [Header("Checkout Door Exit")]
        [Tooltip("World-space offset from the normal counter position to the lobby door destination.")]
        [SerializeField] private Vector2 checkoutDoorTargetOffset = new Vector2(0f, 1.25f);
        [SerializeField] private float checkoutDoorMoveDuration = 0.75f;
        [SerializeField] private float checkoutFadeDuration = 0.35f;

        [Header("Refs (optional)")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Sprite Fit")]
        [SerializeField] private bool fitSpriteToSlot = true;
        [SerializeField] private Vector2 targetSpriteWorldSize = new Vector2(3.1f, 3.35f);

        private float _visibleX;
        private Vector3 _initialScale;
        private float _currentSpriteScaleMultiplier = 1f;
        private Color _initialSpriteColor = Color.white;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) _initialSpriteColor = spriteRenderer.color;
            _initialScale = transform.localScale;
            _visibleX = transform.position.x;
            SetY(hiddenY);
        }

        /// <summary>위치 즉시 리셋 + 새 동물 이미지가 있으면 교체 + 솟아오름.</summary>
        public IEnumerator Spawn(Sprite portrait = null, float spriteScaleMultiplier = 1f)
        {
            PrepareSprite(portrait, spriteScaleMultiplier);
            SetY(hiddenY);
            yield return MoveY(visibleY, riseDuration);
        }

        /// <summary>
        /// 체크아웃 손님을 화면 오른쪽에서 카운터 중앙으로 이동시킨다.
        /// </summary>
        public IEnumerator EnterFromRight(Sprite portrait = null, float spriteScaleMultiplier = 1f)
        {
            PrepareSprite(portrait, spriteScaleMultiplier);

            Vector3 start = new Vector3(_visibleX + Mathf.Abs(checkoutStartOffsetX), visibleY, transform.position.z);
            Vector3 destination = new Vector3(_visibleX, visibleY, transform.position.z);
            transform.position = start;
            yield return MoveTo(destination, checkoutEnterDuration);
        }


        public IEnumerator Sink()
        {
            yield return MoveY(hiddenY, sinkDuration);
        }

        public IEnumerator ExitThroughDoor(Sprite backFacingSprite = null)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (backFacingSprite != null)
                {
                    spriteRenderer.sprite = backFacingSprite;
                    FitSpriteToSlot();
                }
                SetSpriteAlpha(1f);
            }

            Vector3 doorTarget = new Vector3(
                _visibleX + checkoutDoorTargetOffset.x,
                visibleY + checkoutDoorTargetOffset.y,
                transform.position.z
            );

            yield return MoveTo(doorTarget, Mathf.Max(0f, checkoutDoorMoveDuration));
            yield return FadeTo(0f, Mathf.Max(0f, checkoutFadeDuration));

            transform.position = new Vector3(_visibleX, hiddenY, transform.position.z);
        }


        private void SetY(float y)
        {
            var p = transform.position;
            p.y = y;
            transform.position = p;
        }

        private IEnumerator MoveY(float targetY, float duration)
        {
            Vector3 a = transform.position;
            Vector3 b = new Vector3(a.x, targetY, a.z);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                transform.position = Vector3.Lerp(a, b, k);
                yield return null;
            }
            transform.position = b;
        }

        private IEnumerator MoveTo(Vector3 target, float duration)
        {
            Vector3 start = transform.position;
            if (duration <= 0f)
            {
                transform.position = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.position = Vector3.Lerp(start, target, k);
                yield return null;
            }

            transform.position = target;
        }


        private void FitSpriteToSlot()
        {
            if (!fitSpriteToSlot || spriteRenderer == null || spriteRenderer.sprite == null) return;

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float parentScaleX = Mathf.Abs(parentScale.x) > 0f ? Mathf.Abs(parentScale.x) : 1f;
            float parentScaleY = Mathf.Abs(parentScale.y) > 0f ? Mathf.Abs(parentScale.y) : 1f;

            Vector2 adjustedTargetSize = targetSpriteWorldSize * _currentSpriteScaleMultiplier;
            float xScale = adjustedTargetSize.x / (spriteSize.x * parentScaleX);
            float yScale = adjustedTargetSize.y / (spriteSize.y * parentScaleY);
            float uniformScale = Mathf.Min(xScale, yScale);

            transform.localScale = new Vector3(uniformScale, uniformScale, _initialScale.z);
        }

        private void PrepareSprite(Sprite portrait, float spriteScaleMultiplier)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            _currentSpriteScaleMultiplier = Mathf.Max(0.1f, spriteScaleMultiplier);

            if (spriteRenderer == null) return;
            if (portrait != null) spriteRenderer.sprite = portrait;
            SetSpriteAlpha(1f);
            FitSpriteToSlot();
        }

        private void SetSpriteAlpha(float alpha)
        {
            if (spriteRenderer == null) return;
            Color color = _initialSpriteColor;
            color.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (spriteRenderer == null) yield break;

            float startAlpha = spriteRenderer.color.a;
            if (duration <= 0f)
            {
                SetSpriteAlpha(targetAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                SetSpriteAlpha(Mathf.Lerp(startAlpha, targetAlpha, k));
                yield return null;
            }

            SetSpriteAlpha(targetAlpha);
        }
    }
}
