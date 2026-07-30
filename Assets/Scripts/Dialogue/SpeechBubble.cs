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

        [Header("Font")]
        [Tooltip("한글 글리프가 포함된 Font Asset(예: AppleGothic SDF)을 직접 지정한다. " +
                 "비워두면 TMP Settings의 전역 기본 폰트(LiberationSans SDF, 한글 없음)로 떨어져서 " +
                 "매번 Fallback SubMesh가 생기고 sortingOrder가 깨진다 — 반드시 지정할 것.")]
        [SerializeField] private TMP_FontAsset koreanFontAsset;

        [Header("Typing")]
        [SerializeField] private float typeCharsPerSec = 25f;

        [Header("Animal Voice")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioClip typingVoiceSfx;
        [Range(0f, 1f)]
        [SerializeField] private float typingVoiceVolume = 0.3f;

        [Header("Box-Only Mode (텍스트 없을 때 박스만 보여줄 시간)")]
        [SerializeField] private float boxOnlyDuration = 1.5f;

        private void Awake()
        {
            if (label != null)
            {
                label.textWrappingMode = TextWrappingModes.Normal;
            }

            if (voiceSource == null)
                voiceSource = GetComponent<AudioSource>();
            if (voiceSource != null)
                voiceSource.playOnAwake = false;

            HideImmediate();
        }

        public void HideImmediate()
        {
            StopTypingVoice();
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
            // 폰트가 지정 안 돼있으면 한글이 TMP 전역 Fallback을 타면서 SubMesh가 생기고, 그 SubMesh는
            // 이 label의 원래 sortingOrder를 물려받지 못한다. 폰트를 명시하고 sortingOrder를 동기화한다.
            int currentSortingOrder = label.GetComponent<MeshRenderer>()?.sortingOrder ?? 0;
            TMPKoreanFix.Apply(label, koreanFontAsset, currentSortingOrder);
            label.maxVisibleCharacters = 0;
            float charDelay = 1f / Mathf.Max(typeCharsPerSec, 0.1f);

            StartTypingVoice();
            for (int i = 0; i < text.Length; i++)
            {
                label.maxVisibleCharacters = i + 1;
                if (charDelay > 0f) yield return new WaitForSeconds(charDelay);
            }
            StopTypingVoice();
        }

        private void StartTypingVoice()
        {
            if (voiceSource == null || typingVoiceSfx == null)
                return;

            voiceSource.Stop();
            voiceSource.clip = typingVoiceSfx;
            voiceSource.loop = true;
            voiceSource.volume = Mathf.Clamp01(typingVoiceVolume);
            voiceSource.Play();
        }

        private void StopTypingVoice()
        {
            if (voiceSource == null)
                return;

            voiceSource.Stop();
            voiceSource.loop = false;
        }

        public bool IsVisible => backgroundObj != null && backgroundObj.activeSelf;
    }
}
