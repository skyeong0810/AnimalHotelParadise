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
        [SerializeField] private float hiddenY  = -1.5f;
        [SerializeField] private float visibleY =  1.0f;

        [Header("Animation")]
        [SerializeField] private float riseDuration = 0.7f;
        [SerializeField] private float sinkDuration = 0.5f;

        [Header("Refs (optional)")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Sprite Fit")]
        [SerializeField] private bool fitSpriteToSlot = true;
        [SerializeField] private Vector2 targetSpriteWorldSize = new Vector2(3.1f, 3.35f);

        private Vector3 _initialScale;
        private float _currentSpriteScaleMultiplier = 1f;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            _initialScale = transform.localScale;
            SetY(hiddenY);
        }

        /// <summary>위치 즉시 리셋 + 새 동물 이미지가 있으면 교체 + 솟아오름.</summary>
        public IEnumerator Spawn(Sprite portrait = null, float spriteScaleMultiplier = 1f)
        {
            _currentSpriteScaleMultiplier = Mathf.Max(0.1f, spriteScaleMultiplier);
            if (portrait != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = portrait;
                FitSpriteToSlot();
            }

            SetY(hiddenY);
            yield return MoveY(visibleY, riseDuration);
        }

        public IEnumerator Sink()
        {
            yield return MoveY(hiddenY, sinkDuration);
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
    }
}
