using System.Collections;
using UnityEngine;
using AnimalHotel.Counter;

namespace AnimalHotel.Audio
{
    /// <summary>
    /// Background Music (BGM) Manager for Animal Hotel Paradise.
    /// Features:
    ///   - Morning / Afternoon BGM switching with smooth cross-fading.
    ///   - Audio Ducking (volume reduction) during nuisance phone calls / dialogue interrupts.
    ///   - Volume control (Master / BGM) and mute capabilities.
    /// </summary>
    public class BGMManager : MonoBehaviour
    {
        public static BGMManager Instance { get; private set; }

        [Header("BGM Clips")]
        [Tooltip("BGM played during the Morning phase (Diurnal animals arrive).")]
        [SerializeField] private AudioClip morningBgm;

        [Tooltip("BGM played during the Afternoon phase (Nocturnal animals arrive).")]
        [SerializeField] private AudioClip afternoonBgm;

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float bgmVolume = 1f;

        [Header("Transition Settings")]
        [Tooltip("Duration in seconds for cross-fading between morning and afternoon BGMs.")]
        [SerializeField] private float crossfadeDuration = 1.5f;

        [Header("Ducking Settings (Phone Call / Interrupts)")]
        [Tooltip("Volume multiplier applied when ducking is active (e.g. 0.3 = 30% of normal BGM volume).")]
        [Range(0f, 1f)]
        [SerializeField] private float duckingVolumeRatio = 0.3f;

        [Tooltip("Duration in seconds to fade in/out during audio ducking.")]
        [SerializeField] private float duckingFadeDuration = 0.5f;

        [Header("Auto Start")]
        [SerializeField] private bool playOnStart = true;

        // References to scene managers
        [Header("Scene References (Optional - auto-found if unassigned)")]
        [SerializeField] private DayManager dayManager;
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private DialogueManager dialogueManager;

        // Internal AudioSources for dual-channel cross-fading
        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _isSourceAActive = true;

        private Coroutine _crossfadeCoroutine;
        private Coroutine _duckingCoroutine;

        private float _currentDuckingMultiplier = 1f;
        private AudioClip _currentClip;
        private bool _isDucked = false;

        // How much of each source's own track should be audible right now (0..1), driven purely by
        // cross-fade progress — independent of ducking. Final AudioSource.volume is always
        // level * masterVolume * bgmVolume * duckingMultiplier, computed in one place (ApplyVolumes).
        //
        // This replaced a design where the crossfade and ducking coroutines each wrote raw
        // AudioSource.volume values directly, using _isSourceAActive to guess "the active source" —
        // but that flag only flips AFTER a crossfade finishes. A nuisance call ringing WHILE the
        // morning→afternoon ("night") crossfade was still running made the ducking routine dim the
        // OUTGOING source (which the crossfade coroutine was simultaneously fading to 0 anyway) while
        // leaving the INCOMING one undimmed, and the two coroutines fought over the same field every
        // frame. Because nuisance calls only fire in the afternoon/night phase, and are often scheduled
        // shortly after that phase (and its crossfade) begins, this glitch was heard almost exclusively
        // at night — sounding like the BGM abruptly cut out.
        private float _sourceALevel = 0f;
        private float _sourceBLevel = 0f;

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateSourceVolumes();
            }
        }

        public float BgmVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                UpdateSourceVolumes();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            AudioSource[] existingSources = GetComponents<AudioSource>();
            if (existingSources.Length >= 2)
            {
                _sourceA = existingSources[0];
                _sourceB = existingSources[1];
            }
            else
            {
                if (existingSources.Length == 1)
                {
                    _sourceA = existingSources[0];
                }
                else
                {
                    _sourceA = gameObject.AddComponent<AudioSource>();
                }
                _sourceB = gameObject.AddComponent<AudioSource>();
            }

            ConfigureSource(_sourceA);
            ConfigureSource(_sourceB);
        }

        private void ConfigureSource(AudioSource source)
        {
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeEvents();

            if (playOnStart)
            {
                UpdateBGMForCurrentTimeOfDay(immediate: true);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ResolveReferences()
        {
            if (dayManager == null) dayManager = FindFirstObjectByType<DayManager>();
            if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
            if (dialogueManager == null) dialogueManager = FindFirstObjectByType<DialogueManager>();
        }

        private void SubscribeEvents()
        {
            if (dayManager != null)
            {
                dayManager.OnTimeOfDayChanged += HandleTimeOfDayChanged;
            }

            if (roomManager != null)
            {
                roomManager.OnCallRinging += HandleCallRinging;
                roomManager.OnCallEnded += HandleCallEnded;
            }

            if (dialogueManager != null)
            {
                dialogueManager.OnPhoneCallDialogueEnd += HandlePhoneCallDialogueEnd;
            }
        }

        private void UnsubscribeEvents()
        {
            if (dayManager != null)
            {
                dayManager.OnTimeOfDayChanged -= HandleTimeOfDayChanged;
            }

            if (roomManager != null)
            {
                roomManager.OnCallRinging -= HandleCallRinging;
                roomManager.OnCallEnded -= HandleCallEnded;
            }

            if (dialogueManager != null)
            {
                dialogueManager.OnPhoneCallDialogueEnd -= HandlePhoneCallDialogueEnd;
            }
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void HandleTimeOfDayChanged()
        {
            UpdateBGMForCurrentTimeOfDay(immediate: false);
        }

        private void HandleCallRinging(Animal guest, int roomNumber)
        {
            SetDucking(true);
        }

        private void HandleCallEnded(Animal guest, int roomNumber, bool wasAnswered)
        {
            // If the call was missed or timed out (not answered), restore BGM volume immediately —
            // unless another call is already queued to ring on the very next tick (see
            // RoomManager.HasImmediatelyQueuedCall), in which case un-ducking now would just mean
            // ducking again a moment later. Common at night, where the arrival queue drains early and
            // calls fire back-to-back.
            if (!wasAnswered && !HasImmediatelyQueuedCall())
            {
                SetDucking(false);
            }
        }

        private void HandlePhoneCallDialogueEnd(Animal guest, string exitNodeId)
        {
            if (HasImmediatelyQueuedCall())
            {
                // Stay ducked straight through into the next call instead of restoring volume for a
                // fraction of a second only to duck right back down — that reads as a stutter/cut, not
                // two deliberate dips.
                return;
            }
            SetDucking(false);
        }

        private bool HasImmediatelyQueuedCall()
        {
            return roomManager != null && roomManager.HasImmediatelyQueuedCall();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the current BGM track based on DayManager's IsMorning state.
        /// </summary>
        public void UpdateBGMForCurrentTimeOfDay(bool immediate = false)
        {
            AudioClip targetClip = GetTargetClipForTimeOfDay();
            PlayClip(targetClip, immediate ? 0f : crossfadeDuration);
        }

        /// <summary>
        /// Plays a specific AudioClip with optional crossfade duration.
        /// </summary>
        public void PlayClip(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip == null)
            {
                StopBGM(fadeDuration);
                return;
            }

            if (_currentClip == clip && IsPlaying())
            {
                return; // Already playing this track
            }

            _currentClip = clip;
            float duration = fadeDuration >= 0f ? fadeDuration : crossfadeDuration;

            if (_crossfadeCoroutine != null)
            {
                StopCoroutine(_crossfadeCoroutine);
                SettleLevelsToActive();
            }

            _crossfadeCoroutine = StartCoroutine(CrossfadeToClipRoutine(clip, duration));
        }

        /// <summary>
        /// Stops background music playback with an optional fade-out.
        /// </summary>
        public void StopBGM(float fadeDuration = 1.0f)
        {
            _currentClip = null;
            if (_crossfadeCoroutine != null)
            {
                StopCoroutine(_crossfadeCoroutine);
                SettleLevelsToActive();
            }

            _crossfadeCoroutine = StartCoroutine(FadeOutAllRoutine(fadeDuration));
        }

        /// <summary>
        /// Manually sets Audio Ducking (lowers/restores BGM volume).
        /// </summary>
        public void SetDucking(bool duck)
        {
            if (_isDucked == duck) return;
            _isDucked = duck;

            if (_duckingCoroutine != null)
            {
                StopCoroutine(_duckingCoroutine);
            }

            float targetMultiplier = _isDucked ? duckingVolumeRatio : 1f;
            _duckingCoroutine = StartCoroutine(FadeDuckingMultiplierRoutine(targetMultiplier, duckingFadeDuration));
        }

        public bool IsPlaying()
        {
            return (_sourceA != null && _sourceA.isPlaying) || (_sourceB != null && _sourceB.isPlaying);
        }

        // ── Internal Routines & Math ──────────────────────────────────────────────

        private AudioClip GetTargetClipForTimeOfDay()
        {
            if (dayManager != null)
            {
                return dayManager.IsMorning ? morningBgm : afternoonBgm;
            }
            return morningBgm != null ? morningBgm : afternoonBgm;
        }

        private float GetLevel(bool isSourceA) => isSourceA ? _sourceALevel : _sourceBLevel;

        private void SetLevel(bool isSourceA, float value)
        {
            if (isSourceA) _sourceALevel = value;
            else _sourceBLevel = value;
        }

        /// <summary>
        /// Single source of truth for both AudioSources' actual volume: level (crossfade progress) times
        /// the ducking multiplier, applied uniformly to whichever source(s) are currently mid-fade. Both
        /// the crossfade and ducking coroutines route every volume change through this instead of writing
        /// AudioSource.volume directly, so they can never stomp on each other's intent.
        /// </summary>
        private void ApplyVolumes()
        {
            float baseVol = masterVolume * bgmVolume * _currentDuckingMultiplier;
            if (_sourceA != null) _sourceA.volume = _sourceALevel * baseVol;
            if (_sourceB != null) _sourceB.volume = _sourceBLevel * baseVol;
        }

        private void UpdateSourceVolumes()
        {
            ApplyVolumes();
        }

        /// <summary>
        /// Snaps _sourceALevel/_sourceBLevel to a clean "settled" state matching _isSourceAActive (1 for
        /// the active source, 0 for the other) before an in-flight crossfade coroutine gets discarded via
        /// StopCoroutine. StopCoroutine doesn't run the rest of the coroutine body, so an interrupted
        /// crossfade (e.g. two PlayClip calls in quick succession) would otherwise leave the levels at
        /// whatever mid-fade values they were interrupted at — and _isSourceAActive itself wouldn't have
        /// flipped yet either, since that only happens at the tail of CrossfadeToClipRoutine — so the next
        /// crossfade could start from an inconsistent baseline and produce an audible level jump.
        /// </summary>
        private void SettleLevelsToActive()
        {
            SetLevel(_isSourceAActive, 1f);
            SetLevel(!_isSourceAActive, 0f);
            ApplyVolumes();
        }

        private IEnumerator CrossfadeToClipRoutine(AudioClip newClip, float duration)
        {
            bool activeIsA = _isSourceAActive;
            AudioSource activeSource = activeIsA ? _sourceA : _sourceB;
            AudioSource incomingSource = activeIsA ? _sourceB : _sourceA;

            incomingSource.clip = newClip;
            incomingSource.time = 0f;
            incomingSource.Play();

            if (duration <= 0f)
            {
                SetLevel(!activeIsA, 1f);
                SetLevel(activeIsA, 0f);
                ApplyVolumes();
                activeSource.Stop();
            }
            else
            {
                float elapsed = 0f;
                float startActiveLevel = GetLevel(activeIsA);

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    SetLevel(!activeIsA, Mathf.Lerp(0f, 1f, t));
                    SetLevel(activeIsA, Mathf.Lerp(startActiveLevel, 0f, t));
                    ApplyVolumes();

                    yield return null;
                }

                SetLevel(!activeIsA, 1f);
                SetLevel(activeIsA, 0f);
                ApplyVolumes();
                activeSource.Stop();
            }

            _isSourceAActive = !_isSourceAActive;
            _crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutAllRoutine(float duration)
        {
            bool activeIsA = _isSourceAActive;
            AudioSource activeSource = activeIsA ? _sourceA : _sourceB;
            AudioSource inactiveSource = activeIsA ? _sourceB : _sourceA;

            inactiveSource.Stop();
            SetLevel(!activeIsA, 0f);

            if (duration <= 0f)
            {
                SetLevel(activeIsA, 0f);
                ApplyVolumes();
                activeSource.Stop();
            }
            else
            {
                float startLevel = GetLevel(activeIsA);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetLevel(activeIsA, Mathf.Lerp(startLevel, 0f, t));
                    ApplyVolumes();
                    yield return null;
                }

                SetLevel(activeIsA, 0f);
                ApplyVolumes();
                activeSource.Stop();
            }

            _crossfadeCoroutine = null;
        }

        private IEnumerator FadeDuckingMultiplierRoutine(float targetMultiplier, float duration)
        {
            float startMultiplier = _currentDuckingMultiplier;
            float elapsed = 0f;

            if (duration <= 0f)
            {
                _currentDuckingMultiplier = targetMultiplier;
                ApplyVolumes();
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    _currentDuckingMultiplier = Mathf.Lerp(startMultiplier, targetMultiplier, t);
                    ApplyVolumes();
                    yield return null;
                }

                _currentDuckingMultiplier = targetMultiplier;
                ApplyVolumes();
            }

            _duckingCoroutine = null;
        }
    }
}
