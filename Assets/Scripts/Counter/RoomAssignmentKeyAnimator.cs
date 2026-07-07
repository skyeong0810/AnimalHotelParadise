using System.Collections;
using UnityEngine;

namespace AnimalHotel.Counter
{
    public class RoomAssignmentKeyAnimator : MonoBehaviour
    {
        [Header("Hidden Position")]
        [SerializeField] private float hiddenYOffset = 2f;
        [SerializeField] private bool hideOnAwake = true;

        [Header("Animation")]
        [SerializeField] private float riseDuration = 0.45f;
        [SerializeField] private bool fadeInWhileRising = true;

        private Vector3 _visibleLocalPosition;
        private Vector3 _hiddenLocalPosition;
        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private int _motionToken;
        private bool _hasCachedPosition;

        public bool IsShown { get; private set; }

        private void Awake()
        {
            CacheRenderers();
            CachePositions();
            if (hideOnAwake)
                HideImmediate();
        }

        private void OnValidate()
        {
            hiddenYOffset = Mathf.Max(0.1f, hiddenYOffset);
            riseDuration = Mathf.Max(0.05f, riseDuration);
        }

        public void HideImmediate()
        {
            CacheRenderers();
            CachePositions();
            _motionToken++;
            transform.localPosition = _hiddenLocalPosition;
            SetVisible(false);
            SetAlpha(0f);
            IsShown = false;
        }

        public IEnumerator Show()
        {
            CachePositions();

            if (IsShown)
                yield break;

            int token = ++_motionToken;
            SetVisible(true);
            SetAlpha(fadeInWhileRising ? 0f : 1f);
            yield return MoveTo(_visibleLocalPosition, riseDuration, token);
            if (token == _motionToken)
            {
                SetAlpha(1f);
                IsShown = true;
            }
        }

        private void CachePositions()
        {
            if (_hasCachedPosition)
                return;

            _visibleLocalPosition = transform.localPosition;
            _hiddenLocalPosition = _visibleLocalPosition + Vector3.down * hiddenYOffset;
            _hasCachedPosition = true;
        }

        private void CacheRenderers()
        {
            if (_renderers != null)
                return;

            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
            }
        }

        private IEnumerator MoveTo(Vector3 target, float duration, int token)
        {
            Vector3 start = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (token != _motionToken)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                transform.localPosition = Vector3.Lerp(start, target, eased);
                if (fadeInWhileRising)
                    SetAlpha(eased);
                yield return null;
            }

            if (token == _motionToken)
                transform.localPosition = target;
        }

        private void SetVisible(bool isVisible)
        {
            if (_renderers == null)
                return;

            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                    renderer.enabled = isVisible;
            }
        }

        private void SetAlpha(float alpha)
        {
            if (_renderers == null || _baseColors == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null)
                    continue;

                Color color = i < _baseColors.Length ? _baseColors[i] : _renderers[i].color;
                color.a *= Mathf.Clamp01(alpha);
                _renderers[i].color = color;
            }
        }
    }
}
