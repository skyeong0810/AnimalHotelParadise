using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 답변 옵션 풍선. 옵션 클릭 시 ui click 사운드 재생.
    /// 클릭/호버는 Update 기반 (새 Input System / 옛 Input Manager 둘 다 지원).
    /// </summary>
    public class SimpleResponseBubble : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject backgroundObj;
        [SerializeField] private Transform optionContainer;
        [SerializeField] private Sprite buttonSprite;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("답변 옵션 버튼 클릭 시 재생되는 사운드")]
        [SerializeField] private AudioClip clickSfx;
        [Range(0f, 1f)] [SerializeField] private float masterSfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float clickVolume = 1f;

        [Header("Button Layout")]
        [SerializeField] private float buttonWidth = 4.0f;
        [SerializeField] private float buttonHeight = 0.65f;
        [SerializeField] private float buttonSpacing = 0.75f;
        [SerializeField] private float labelFontSize = 0.35f;

        [Header("Sorting Orders")]
        [SerializeField] private int bgSortingOrder = 6;
        [SerializeField] private int textSortingOrder = 7;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(1f, 0.95f, 0.6f, 0.95f);
        [SerializeField] private Color hoverColor  = new Color(1f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color labelColor  = new Color(0.15f, 0.05f, 0.10f);

        public event Action<int> OnOptionSelected;
        public bool IsVisible => backgroundObj != null && backgroundObj.activeSelf;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private SimpleOptionButton _currentHover;
        private Camera _cachedCam;

        private void Awake() => HideImmediate();

        public void HideImmediate()
        {
            ClearButtons();
            if (backgroundObj != null) backgroundObj.SetActive(false);
            _currentHover = null;
        }

        public void Show(List<string> options)
        {
            ClearButtons();
            if (backgroundObj != null) backgroundObj.SetActive(true);
            if (options == null || options.Count == 0) return;

            float totalH = (options.Count - 1) * buttonSpacing;
            float startY = totalH / 2f;
            for (int i = 0; i < options.Count; i++)
            {
                float y = startY - i * buttonSpacing;
                _spawned.Add(CreateButton(options[i], i, y));
            }
        }

        public void Show(int count)
        {
            var list = new List<string>(count);
            for (int i = 0; i < count; i++) list.Add("");
            Show(list);
        }

        private GameObject CreateButton(string text, int index, float localY)
        {
            var go = new GameObject("Option_" + index);
            go.transform.SetParent(optionContainer != null ? optionContainer : transform);
            go.transform.localPosition = new Vector3(0, localY, 0);
            go.transform.localScale = Vector3.one;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(buttonWidth, buttonHeight);

            var bg = new GameObject("Bg");
            bg.transform.SetParent(go.transform);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = new Vector3(buttonWidth, buttonHeight, 1);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = buttonSprite;
            sr.color = normalColor;
            sr.sortingOrder = bgSortingOrder;

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform);
            lbl.transform.localPosition = new Vector3(0, 0, -0.01f);
            lbl.transform.localScale = Vector3.one;
            var tmp = lbl.AddComponent<TextMeshPro>();
            tmp.text = text != null ? text : "";
            tmp.fontSize = labelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = labelColor;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            var rt = tmp.rectTransform;
            rt.sizeDelta = new Vector2(buttonWidth * 0.92f, buttonHeight * 0.92f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var mr = lbl.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = textSortingOrder;

            var btn = go.AddComponent<SimpleOptionButton>();
            btn.Setup(index, sr, normalColor, hoverColor, OnButtonClicked);
            return go;
        }

        private void OnButtonClicked(int index)
        {
            PlaySfx(clickSfx, clickVolume);
            OnOptionSelected?.Invoke(index);
            HideImmediate();
        }

private void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (sfxSource != null && clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * masterSfxVolume));
        }

        private void ClearButtons()
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawned.Clear();
            _currentHover = null;
        }

        private void Update()
        {
            if (!IsVisible || _spawned.Count == 0) { ClearHover(); return; }
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;

            Vector2 mousePos; bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 worldPoint = new Vector2(w3.x, w3.y);
            var hitCol = Physics2D.OverlapPoint(worldPoint);
            SimpleOptionButton hitBtn = hitCol != null ? hitCol.GetComponent<SimpleOptionButton>() : null;

            if (hitBtn != _currentHover)
            {
                if (_currentHover != null) _currentHover.SetHover(false);
                if (hitBtn != null) hitBtn.SetHover(true);
                _currentHover = hitBtn;
            }
            if (mouseDown && hitBtn != null) { hitBtn.HandleClick(); _currentHover = null; }
        }

        private void ClearHover() { if (_currentHover != null) { _currentHover.SetHover(false); _currentHover = null; } }

        private static void GetMouseInput(out Vector2 pos, out bool downThisFrame)
        {
            pos = Vector2.zero; downThisFrame = false;
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null)
            {
                pos = pointer.position.ReadValue();
                downThisFrame = pointer.press.wasPressedThisFrame;
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            pos = Input.mousePosition; downThisFrame = Input.GetMouseButtonDown(0);
#endif
        }
    }

    public class SimpleOptionButton : MonoBehaviour
    {
        private int _index;
        private SpriteRenderer _sr;
        private Color _normal;
        private Color _hover;
        private Action<int> _onClick;

        private bool _isEnabled = true;
        private TextMeshPro _tmp;
        private Color _textNormalColor;
        private Collider2D _collider;

        public void Setup(int index, SpriteRenderer sr, Color normal, Color hover, Action<int> onClick)
        {
            _index = index;
            _sr = sr;
            _normal = normal;
            _hover = hover;
            _onClick = onClick;
            _collider = GetComponent<Collider2D>();
            _isEnabled = true;
            SetHover(false);
        }

        public void Setup(int index, SpriteRenderer sr, TextMeshPro tmp, Color normal, Color hover, Color textNormal, Action<int> onClick, bool isEnabled = true)
        {
            _index = index;
            _sr = sr;
            _tmp = tmp;
            _normal = normal;
            _hover = hover;
            _textNormalColor = textNormal;
            _onClick = onClick;
            _collider = GetComponent<Collider2D>();

            SetEnabled(isEnabled);
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (_collider == null) _collider = GetComponent<Collider2D>();
            if (_collider != null) _collider.enabled = enabled;

            if (!_isEnabled)
            {
                if (_sr != null) _sr.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                if (_tmp != null) _tmp.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            }
            else
            {
                if (_sr != null) _sr.color = _normal;
                if (_tmp != null) _tmp.color = _textNormalColor;
            }
        }

        public void SetHover(bool hover)
        {
            if (!_isEnabled) return;
            if (_sr != null) _sr.color = hover ? _hover : _normal;
        }

        public void HandleClick()
        {
            if (!_isEnabled) return;
            _onClick?.Invoke(_index);
        }

        public string GetText()
        {
            return _tmp != null ? _tmp.text : "";
        }
    }
}
