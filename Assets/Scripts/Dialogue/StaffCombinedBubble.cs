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

        [Header("Font")]
        [Tooltip("한글 글리프가 포함된 Font Asset(예: AppleGothic SDF)을 직접 지정한다. " +
                 "비워두면 TMP Settings의 전역 기본 폰트(LiberationSans SDF, 한글 없음)로 떨어져서 " +
                 "매번 Fallback SubMesh가 생기고 sortingOrder가 깨진다 — 반드시 지정할 것.")]
        [SerializeField] private TMP_FontAsset koreanFontAsset;

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
        [Tooltip("Moves the staff line and choices together inside the bubble.")]
        [SerializeField] private float contentYOffset = 0.35f;

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
        // Line 섹션은 lineText가 실제로 있을 때만 공간을 차지한다. TabletCheck 이후 곧바로 선택지만
        // 나오는 노드(예: staff_reservation_choices)처럼 text가 비어있는 경우, 예전에는 빈 Line 영역이
        // 그대로 첫 번째 섹션 자리를 차지해서 선택지 위에 부자연스러운 여백이 생겼다. hasLine이 false면
        // Line Label을 아예 비활성화하고, 배경 높이와 선택지 시작 위치 계산에서도 Line 몫을 완전히 뺀다.
        public IEnumerator ShowLineWithChoices(string lineText, List<string> options, List<bool> optionStates = null)
        {
            ClearButtons();
            int choiceCount = (options != null) ? options.Count : 0;
            bool hasLine = !string.IsNullOrWhiteSpace(lineText);
            ShowBackground(choiceCount, hasLine);

            if (lineLabel != null)
            {
                lineLabel.gameObject.SetActive(hasLine);
                if (hasLine)
                {
                    PositionLineLabel(choiceCount, hasLine);
                    lineLabel.text = lineText;
                    TMPKoreanFix.Apply(lineLabel, koreanFontAsset, textSortingOrder);
                }
                else
                {
                    lineLabel.text = "";
                }
            }

            if (options != null && options.Count > 0)
            {
                float totalH = CalcTotalHeight(choiceCount, hasLine);
                float startY;
                if (hasLine)
                {
                    float lineBottom = (totalH / 2f) - lineHeight;
                    startY = lineBottom - lineToChoiceGap;
                }
                else
                {
                    // Line이 없으면 선택지 그룹을 배경 안에서 그대로 수직 중앙 정렬한다
                    // (버튼 n개가 (n-1)*buttonSpacing 간격으로 0을 중심으로 대칭 배치됨).
                    startY = (totalH / 2f) - (buttonSpacing / 2f);
                }
                for (int i = 0; i < options.Count; i++)
                {
                    float y = startY - i * buttonSpacing + contentYOffset;
                    bool isEnabled = (optionStates != null && i < optionStates.Count) ? optionStates[i] : true;
                    _spawnedButtons.Add(CreateButton(options[i], i, y, isEnabled));
                }
            }

            _waitingForChoice = true;
            _selectedIndex = -1;
            yield return new WaitUntil(() => !_waitingForChoice);
        }

        // === 배경 크기 자동 조절 ===
        private float CalcTotalHeight(int choiceCount, bool hasLine = true)
        {
            float h = hasLine ? lineHeight : 0f;
            if (choiceCount > 0)
                h += (hasLine ? lineToChoiceGap : 0f) + choiceCount * buttonSpacing;
            return h;
        }

        private void ShowBackground(int choiceCount, bool hasLine = true)
        {
            if (backgroundObj == null) return;
            backgroundObj.SetActive(true);
            if (_bgRenderer != null) _bgRenderer.color = bgColor;
            float totalH = CalcTotalHeight(choiceCount, hasLine) + bgPaddingY * 2f;
            float totalW = buttonWidth + bgPaddingX * 2f;
            if (_bgRenderer != null)
                _bgRenderer.size = new Vector2(totalW, totalH);
            else
                backgroundObj.transform.localScale = new Vector3(totalW, totalH, 1f);
        }

        private void PositionLineLabel(int choiceCount, bool hasLine = true)
        {
            if (lineLabel == null) return;
            float totalH = CalcTotalHeight(choiceCount, hasLine);
            float topY = totalH / 2f - lineHeight / 2f;
            lineLabel.transform.localPosition = new Vector3(0f, topY + contentYOffset, -0.01f);
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
            TMPKoreanFix.Apply(tmp, koreanFontAsset, textSortingOrder);
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
            // AddComponent로 새로 만든 TextMeshPro는 font가 지정되지 않으면 TMP Settings의 전역 기본
            // 폰트(LiberationSans SDF, 한글 없음)로 떨어져서 한글을 그릴 때마다 Fallback SubMesh가
            // 생긴다. text를 채우기 "전에" 먼저 한글 폰트를 지정해서 애초에 그 경로를 타지 않게 한다.
            if (koreanFontAsset != null) tmp.font = koreanFontAsset;
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
            // 그래도 SubMesh가 생겼을 경우(예: 폰트에 없는 특수문자)를 대비해 sortingLayer/Order를
            // 원본 렌더러 기준으로 강제 동기화한다.
            TMPKoreanFix.Apply(tmp, null, textSortingOrder);
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
            AnimalHotel.InputHelper.GetInput(out pos, out downThisFrame);
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
