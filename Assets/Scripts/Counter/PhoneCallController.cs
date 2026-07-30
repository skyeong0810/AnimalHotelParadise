using System.Collections;
using UnityEngine;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// Controls the Call parent object and its Phone and PhoneLine children in CounterScene.
    /// Deactivated normally. When an incoming call arrives, activates the object,
    /// sets it down below, rises it up to its initial position, rings the Phone object,
    /// and sinks/deactivates when answered or timed out.
    /// </summary>
    public class PhoneCallController : MonoBehaviour
    {
        [Header("Hierarchy References")]
        [Tooltip("The parent 'Call' GameObject. Defaults to this object if unassigned.")]
        [SerializeField] private GameObject callObject;

        [Tooltip("The child 'Phone' Transform.")]
        [SerializeField] private Transform phoneTransform;

        [Tooltip("The child 'PhoneLine' Transform.")]
        [SerializeField] private Transform phoneLineTransform;

        [Header("Animation Settings")]
        [Tooltip("How far below the initial position the Call object is placed before rising.")]
        [SerializeField] private float hiddenYOffset = 2.0f;
        [SerializeField] private float riseDuration = 0.4f;
        [SerializeField] private float sinkDuration = 0.4f;

        [Header("Ringing Duration")]
        [Tooltip("How many seconds an unanswered incoming call will ring before being cancelled.")]
        [SerializeField] private float callRingingDurationSeconds = 10f;

        [Header("Ring Vibration")]
        [Tooltip("Rotation shake intensity (in degrees) while ringing.")]
        [SerializeField] private float ringShakeAngle = 7.0f;
        [Tooltip("Speed of the ring rotation shake.")]
        [SerializeField] private float ringShakeSpeed = 25.0f;

        [Header("Audio")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip ringtoneSfx;
        [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;

        private Vector3 _visibleLocalPos;
        private Vector3 _hiddenLocalPos;
        private Quaternion _phoneInitialRotation;
        private Coroutine _motionCoroutine;
        private bool _isRinging;
        private float _ringingTimer;

        public float CallRingingDurationSeconds => callRingingDurationSeconds;

        private void Awake()
        {
            if (callObject == null) callObject = gameObject;

            if (phoneTransform == null)
            {
                Transform phoneChild = transform.Find("Phone");
                if (phoneChild != null) phoneTransform = phoneChild;
            }

            if (phoneLineTransform == null)
            {
                Transform lineChild = transform.Find("PhoneLine");
                if (lineChild != null) phoneLineTransform = lineChild;
            }

            if (phoneTransform != null)
            {
                _phoneInitialRotation = phoneTransform.localRotation;
            }

            // Cache the initial position set in the scene (where it should go when call comes)
            _visibleLocalPos = callObject.transform.localPosition;
            _hiddenLocalPos = _visibleLocalPos - new Vector3(0, hiddenYOffset, 0);

            // Automatically setup click forwarders on all child colliders (Phone, PhoneLine, etc.)
            SetupColliderForwarders();

            // Deactivate normally on Awake
            callObject.SetActive(false);
        }

        private void SetupColliderForwarders()
        {
            if (callObject == null) return;
            var colliders = callObject.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in colliders)
            {
                if (col != null)
                {
                    var forwarder = col.gameObject.GetComponent<PhoneClickForwarder>();
                    if (forwarder == null)
                    {
                        forwarder = col.gameObject.AddComponent<PhoneClickForwarder>();
                    }
                    forwarder.Init(this);
                }
            }
        }

        private void Update()
        {
            if (_isRinging)
            {
                // Perform phone rotation shake while ringing
                if (phoneTransform != null)
                {
                    float zAngle = Mathf.Sin(Time.time * ringShakeSpeed) * ringShakeAngle;
                    phoneTransform.localRotation = _phoneInitialRotation * Quaternion.Euler(0, 0, zAngle);
                }

                // Direct raycast click check for 2D colliders
                if (Input.GetMouseButtonDown(0))
                {
                    Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                    if (cam != null)
                    {
                        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(Input.mousePosition);
                        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);
                        if (hit.collider != null && (hit.collider.transform.IsChildOf(callObject.transform) || hit.collider.gameObject == callObject))
                        {
                            AnswerCurrentCall();
                            return;
                        }
                    }
                }

                // Count down ringing duration when time is flowing
                if (DayManager.Instance == null || DayManager.Instance.IsTimeFlowing)
                {
                    _ringingTimer -= Time.deltaTime;
                    if (_ringingTimer <= 0f)
                    {
                        var roomManager = FindFirstObjectByType<RoomManager>();
                        if (roomManager != null)
                        {
                            var activeCall = roomManager.GetActiveRingingCall();
                            if (activeCall != null)
                            {
                                roomManager.CancelCall(activeCall.sufferingGuest);
                            }
                        }
                    }
                }
            }
        }

        public void OnCallRinging(Animal guest, int roomNumber)
        {
            if (_isRinging) return;
            _isRinging = true;
            _ringingTimer = callRingingDurationSeconds;
            if (callObject != null && !callObject.activeSelf) callObject.SetActive(true);

            // Re-bind forwarders when activated
            SetupColliderForwarders();

            if (_motionCoroutine != null) StopCoroutine(_motionCoroutine);
            _motionCoroutine = StartCoroutine(RiseAndRingRoutine());
        }

        public void OnCallEnded(Animal guest, int roomNumber, bool wasAnswered)
        {
            if (!_isRinging && (callObject == null || !callObject.activeSelf)) return;
            _isRinging = false;

            if (_motionCoroutine != null) StopCoroutine(_motionCoroutine);
            _motionCoroutine = StartCoroutine(SinkAndDeactivateRoutine());
        }

        private IEnumerator RiseAndRingRoutine()
        {
            // Activate GameObject and place down below
            callObject.SetActive(true);
            callObject.transform.localPosition = _hiddenLocalPos;

            float elapsed = 0f;

            if (sfxSource != null && ringtoneSfx != null)
            {
                sfxSource.clip = ringtoneSfx;
                sfxSource.volume = sfxVolume;
                sfxSource.loop = true;
                sfxSource.Play();
            }

            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                t = t * (2f - t); // Ease out quad
                callObject.transform.localPosition = Vector3.Lerp(_hiddenLocalPos, _visibleLocalPos, t);
                yield return null;
            }

            callObject.transform.localPosition = _visibleLocalPos;
            _motionCoroutine = null;
        }

        private IEnumerator SinkAndDeactivateRoutine()
        {
            if (sfxSource != null && sfxSource.isPlaying)
            {
                sfxSource.Stop();
            }

            float elapsed = 0f;
            Vector3 startPos = callObject.transform.localPosition;

            while (elapsed < sinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / sinkDuration);
                t = t * t; // Ease in quad
                callObject.transform.localPosition = Vector3.Lerp(startPos, _hiddenLocalPos, t);
                yield return null;
            }

            callObject.transform.localPosition = _visibleLocalPos;

            if (phoneTransform != null)
            {
                phoneTransform.localRotation = _phoneInitialRotation;
            }

            // Deactivate GameObject in hierarchy
            callObject.SetActive(false);
            _motionCoroutine = null;
        }

        /// <summary>
        /// Allows answering the phone by clicking on its collider directly.
        /// </summary>
        private void OnMouseDown()
        {
            AnswerCurrentCall();
        }

        public void AnswerCurrentCall()
        {
            var roomManager = FindFirstObjectByType<RoomManager>();
            if (roomManager != null && _isRinging)
            {
                var ringingCall = roomManager.GetActiveRingingCall();
                if (ringingCall != null)
                {
                    Animal guest = ringingCall.sufferingGuest;
                    int roomNum = ringingCall.roomNumber;

                    bool answered = roomManager.AnswerCall(guest);
                    if (answered)
                    {
                        var dialogueMgr = FindFirstObjectByType<DialogueManager>();
                        if (dialogueMgr != null)
                        {
                            dialogueMgr.StartPhoneCallDialogue(guest, roomNum);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Forwards 2D collider clicks on child GameObjects (e.g. Phone, PhoneLine) to PhoneCallController.
    /// </summary>
    public class PhoneClickForwarder : MonoBehaviour
    {
        private PhoneCallController _controller;

        public void Init(PhoneCallController controller)
        {
            _controller = controller;
        }

        private void OnMouseDown()
        {
            if (_controller != null)
            {
                _controller.AnswerCurrentCall();
            }
        }
    }
}
