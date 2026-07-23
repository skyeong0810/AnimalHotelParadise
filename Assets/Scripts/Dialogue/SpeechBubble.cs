using System.Collections;
using UnityEngine;
using TMPro;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 말풍선. 배경(SpriteRenderer) + 선택적 텍스트(TMP).
    /// label이 null이거나 text가 비어있으면 박스만 잠깐 보여주고 종료.
    /// </summary>
    public class SpeechBubble : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject backgroundObj;
        [SerializeField] private TMP_Text label;

        [Header("Typing")]
        [SerializeField] private float typeCharsPerSec = 25f;

        [Header("Box-Only Mode (텍스트 없을 때 박스만 보여줄 시간)")]
        [SerializeField] private float boxOnlyDuration = 1.5f;

        private void Awake()
        {
            if (label != null)
            {
                label.textWrappingMode = TextWrappingModes.Normal;
            }

            HideImmediate();
        }

        public void HideImmediate()
        {
            StopAllCoroutines();
            if (backgroundObj != null) backgroundObj.SetActive(false);
            if (label != null) label.text = string.Empty;
        }

        public IEnumerator ShowWithText(string text)
        {
            if (backgroundObj != null) backgroundObj.SetActive(true);
            if (label == null || string.IsNullOrEmpty(text))
            {
                yield return new WaitForSeconds(boxOnlyDuration);
                yield break;
            }
            yield return TypeText(text);
        }

        public IEnumerator TypeText(string text)
        {
            if (label == null) yield break;
            label.text = text;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.maxVisibleCharacters = 0;
            float charDelay = 1f / Mathf.Max(typeCharsPerSec, 0.1f);
            for (int i = 0; i < text.Length; i++)
            {
                label.maxVisibleCharacters = i + 1;
                if (charDelay > 0f) yield return new WaitForSeconds(charDelay);
            }
        }

        public bool IsVisible => backgroundObj != null && backgroundObj.activeSelf;
    }
}
