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

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            SetY(hiddenY);
        }

        /// <summary>위치 즉시 리셋 + 새 동물 이미지가 있으면 교체 + 솟아오름.</summary>
        public IEnumerator Spawn(Sprite portrait = null)
        {
            if (portrait != null && spriteRenderer != null)
                spriteRenderer.sprite = portrait;

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
    }
}
