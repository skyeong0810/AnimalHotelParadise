using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AnimalHotel.Counter
{
    public class StaffCombinedBubble : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] private GameObject backgroundObj;
        [SerializeField] private Sprite buttonSprite;

        [Header("Staff Line")]
        [SerializeField] private TextMeshPro lineLabel;

        [Header("Button Layout")]
        [SerializeField] private Transform optionContainer;
        [SerializeField] private float buttonWidth = 4.0f;
        [SerializeField] private float buttonHeight = 0.9f;
        [SerializeField] private float buttonSpacing = 0.75f;
        [SerializeField] private float labelFontSize = 3f;

        [Header("Background Sizing")]
        [SerializeField] private float bgPaddingX = 0.6f;
        [SerializeField] private float bgPaddingY = 0.5f;
        [SerializeField] private float lineHeight = 1.0f;
        [SerializeField] private float lineToChoiceGap = 0.3f;

        [Header("Sorting Orders")]
        [SerializeField] private int buttonBgSortingOrder = 6;
        [SerializeField] private int textSortingOrder = 7;

        [Header("Colors")]
        [SerializeField] private Color bgColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private Color buttonNormalColor = new Color(0.5f, 0.5f, 0.5f, 0.95f);
        [SerializeField] private Color buttonHoverColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        [SerializeField] private Color lineTextColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color buttonTextColor = new Color(1f, 1f, 1f, 1f);

        [Header("Typing Effect")]
        [SerializeField] private float charsPerSecond = 20f;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip clickSfx;
        [Range(0f, 1f)] [SerializeField] private float masterSfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float clickVolume = 1f;

        [Header("Tablet Input Guard")]
        [SerializeField] private TabletController tabletController;
        [SerializeField] private bool blockInputWhileTabletOpen = true;

        public event Action<int> OnOptionSelected;
        public bool IsVisible => backgroundObj != null && backgroundObj.activeSelf;
        public int SelectedIndex => _selectedIndex;

        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
        private SimpleOptionButton _currentHover;
        private Camera _cachedCam;
        private bool _waitingForChoice;
        private int _selectedIndex;
        private SpriteRenderer _bgRenderer;

        private void Awake()
        {
            if (backgroundObj != null) _bgRenderer = backgroundObj.GetComponent<SpriteRenderer>();
            if (tabletController == null) tabletController = FindFirstObjectByType<TabletController>();
            HideImmediate();
        }

        // === 대사만 표시 ===
        public IEnumerator ShowLine(string text)
        {
            ClearButtons();
            ShowBackground(0);
            if (lineLabel != null)
            {
                lineLabel.gameObject.SetActive(true);
                PositionLineLabel(0);
                yield return TypeText(lineLabel, text);
            }
        }

        // === 대사 + 선택지 ===
        public IEnumerator ShowLineWithChoices(string lineText, List<string> options, List<bool> optionStates = null)
        {
            ClearButtons();
            int choiceCount = (options != null) ? options.Count : 0;
            ShowBackground(choiceCount);

            if (lineLabel != null)
            {
                lineLabel.gameObject.SetActive(true);
                PositionLineLabel(choiceCount);
                lineLabel.text = lineText != null ? lineText : "";
            }

            if (options != null && options.Count > 0)
            {
                float totalH = CalcTotalHeight(options.Count);
                float lineBottom = (totalH / 2f) - lineHeight;
                float startY = lineBottom - lineToChoiceGap;
                for (int i = 0; i < options.Count; i++)
                {
                    float y = startY - i * buttonSpacing;
                    bool isEnabled = (optionStates != null && i < optionStates.Count) ? optionStates[i] : true;
                    _spawnedButtons.Add(CreateButton(options[i], i, y, isEnabled));
                }
            }

            _waitingForChoice = true;
            _selectedIndex = -1;
            yield return new WaitUntil(() => !_waitingForChoice);
        }

        // === 배경 크기 자동 조절 ===
        private float CalcTotalHeight(int choiceCount)
        {
            float h = lineHeight;
            if (choiceCount > 0)
                h += lineToChoiceGap + choiceCount * buttonSpacing;
            return h;
        }

        private void ShowBackground(int choiceCount)
        {
            if (backgroundObj == null) return;
            backgroundObj.SetActive(true);
            if (_bgRenderer != null) _bgRenderer.color = bgColor;
            float totalH = CalcTotalHeight(choiceCount) + bgPaddingY * 2f;
            float totalW = buttonWidth + bgPaddingX * 2f;
            backgroundObj.transform.localScale = new Vector3(totalW, totalH, 1f);
        }

        private void PositionLineLabel(int choiceCount)
        {
            if (lineLabel == null) return;
            float totalH = CalcTotalHeight(choiceCount);
            float topY = totalH / 2f - lineHeight / 2f;
            lineLabel.transform.localPosition = new Vector3(0f, topY, -0.01f);
            lineLabel.rectTransform.sizeDelta = new Vector2(buttonWidth * 0.92f, lineHeight);
            lineLabel.color = lineTextColor;
            lineLabel.alignment = TMPro.TextAlignmentOptions.Center;
            lineLabel.fontSize = labelFontSize;
            var mr = lineLabel.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = textSortingOrder;
        }

        public void HideImmediate()
        {
            ClearButtons();
            if (lineLabel != null) { lineLabel.text = ""; lineLabel.gameObject.SetActive(false); }
            if (backgroundObj != null) backgroundObj.SetActive(false);
            _currentHover = null;
        }

        private IEnumerator TypeText(TextMeshPro tmp, string fullText)
        {
            if (string.IsNullOrEmpty(fullText)) { tmp.text = ""; yield break; }
            tmp.text = fullText;
            tmp.maxVisibleCharacters = 0;
            float interval = charsPerSecond > 0 ? 1f / charsPerSecond : 0f;
            for (int i = 0; i < fullText.Length; i++)
            {
                tmp.maxVisibleCharacters = i + 1;
                if (interval > 0f) yield return new WaitForSeconds(interval);
            }
        }

        private GameObject CreateButton(string text, int index, float localY, bool isEnabled = true)
        {
            var parent = optionContainer != null ? optionContainer : transform;
            var go = new GameObject("Option_" + index);
            go.transform.SetParent(parent);
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
            sr.color = buttonNormalColor;
            sr.sortingOrder = buttonBgSortingOrder;
            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform);
            lbl.transform.localPosition = new Vector3(0, 0, -0.01f);
            lbl.transform.localScale = Vector3.one;
            var tmp = lbl.AddComponent<TextMeshPro>();
            tmp.text = text != null ? text : "";
            tmp.fontSize = labelFontSize;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = labelFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = buttonTextColor;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.rectTransform.sizeDelta = new Vector2(buttonWidth * 0.9f, buttonHeight);
            tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var mr = lbl.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = textSortingOrder;
            var btn = go.AddComponent<SimpleOptionButton>();
            btn.Setup(index, sr, tmp, buttonNormalColor, buttonHoverColor, buttonTextColor, OnButtonClicked, isEnabled);
            return go;
        }

        public void EnableAssignChoices()
        {
            foreach (var go in _spawnedButtons)
            {
                if (go == null) continue;
                var btn = go.GetComponent<SimpleOptionButton>();
                if (btn != null)
                {
                    string text = btn.GetText();
                    if (text.Contains("방 배정") || text.Contains("배정해"))
                    {
                        btn.SetEnabled(true);
                    }
                }
            }
        }

        private void OnButtonClicked(int index)
        {
            PlaySfx(clickSfx, clickVolume);
            _selectedIndex = index;
            _waitingForChoice = false;
            OnOptionSelected?.Invoke(index);
            ClearButtons();
        }

        private void ClearButtons()
        {
            foreach (var go in _spawnedButtons)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            _spawnedButtons.Clear();
            _currentHover = null;
        }

        private void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (sfxSource != null && clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume * masterSfxVolume));
        }

        private void Update()
        {
            if (IsTabletBlockingInput())
            {
                ClearHover();
                return;
            }

            if (!IsVisible || _spawnedButtons.Count == 0) { ClearHover(); return; }
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;
            Vector2 mousePos; bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            Vector2 wp = new Vector2(w3.x, w3.y);
            SimpleOptionButton hitBtn = FindHitOptionButton(wp);
            if (hitBtn != _currentHover) { if (_currentHover != null) _currentHover.SetHover(false); if (hitBtn != null) hitBtn.SetHover(true); _currentHover = hitBtn; }
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
    

        private SimpleOptionButton FindHitOptionButton(Vector2 worldPoint)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;
                SimpleOptionButton candidate = hits[i].GetComponent<SimpleOptionButton>();
                if (candidate != null && _spawnedButtons.Contains(candidate.gameObject))
                {
                    return candidate;
                }
            }

            return null;
        }
    

        private bool IsTabletBlockingInput()
        {
            return blockInputWhileTabletOpen && tabletController != null && tabletController.IsOpen;
        }
    }
}
