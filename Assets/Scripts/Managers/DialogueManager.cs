using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalHotel.Counter
{
    public class DialogueManager : MonoBehaviour
    {
        [Header("Bubbles")]
        [SerializeField] private SpeechBubble customerBubble;
        [SerializeField] private StaffCombinedBubble staffBubble;

        [Header("Phone Call Bubbles")]
        [Tooltip("전화 대화 전용 Bubble. 카운터 Bubble과 별도 GameObject여야 하며, sortingOrder를 카운터 Bubble보다 높게 설정해서 그 위에 겹쳐 보이도록(Cover) 한다. 인터럽트 중에도 카운터 Bubble은 숨기지 않는다.")]
        [SerializeField] private SpeechBubble phoneCustomerBubble;
        [SerializeField] private StaffCombinedBubble phoneStaffBubble;

        [Header("Tablet")]
        [SerializeField] private TabletController tabletController;

        [Header("Room")]
        [Tooltip("전화가 '방을 옮겨드리겠다'로 끝났을 때, 그 재배정이 실제로 끝날 때까지 중단됐던 체크인 대화의 재개를 미루기 위해 필요하다.")]
        [SerializeField] private RoomManager roomManager;

        [Header("Timing")]
        [SerializeField] private float delayBetweenNodes = 0.3f;
        [SerializeField] private float delayAfterLine = 1.2f;
        [SerializeField] private float tabletCheckMinDelay = 0.5f;

        public event Action<string> OnDialogueEnd;
        public event Action<string> OnChoiceResolved;

        /// <summary>
        /// Raised specifically when a phone-call complaint dialogue finishes, with the guest and the
        /// exit node id reached (e.g. "phone_exit_move" / "phone_exit_no_move"). Deliberately separate
        /// from OnDialogueEnd: that event is awaited by CounterFlow's check-in coroutine, and firing it
        /// for a phone call as well would incorrectly resolve an unrelated, interrupted check-in wait.
        /// </summary>
        public event Action<Animal, string> OnPhoneCallDialogueEnd;

        public Animal CurrentGuest { get; private set; }

        private Dictionary<string, DialogueNode> _nodes;
        private DialogueNode _currentNode;
        private bool _isRunning;
        private bool _isPhoneCall;
        private bool _roomAssigned = false;

        // Saved Check-in state during phone call
        private bool _hasSavedCheckIn;
        private Animal _savedGuest;
        private Dictionary<string, DialogueNode> _savedNodes;
        private DialogueNode _savedCurrentNode;
        private bool _savedRoomAssigned;

        // Pending fresh start requested while a phone call is in progress (no counter dialogue was
        // running yet, so there's nothing to "resume" — this is a brand-new StartDialogue call that
        // arrived mid-call and must be deferred until the call ends).
        private bool _hasPendingStart;
        private Animal _pendingGuest;
        private bool _pendingClaimsReservation;

        /// <summary>현재 진행 중인 대화 종류(카운터/전화)에 맞는 Bubble을 반환한다.</summary>
        private SpeechBubble ActiveCustomerBubble => _isPhoneCall ? phoneCustomerBubble : customerBubble;
        private StaffCombinedBubble ActiveStaffBubble => _isPhoneCall ? phoneStaffBubble : staffBubble;

        /// <summary>
        /// Call this whenever a room gets assigned to <paramref name="guest"/>, so the "방 배정"
        /// choice can be unlocked for whichever dialogue context that guest actually owns.
        ///
        /// Previously this took no parameter and always acted on "whatever _isPhoneCall says is
        /// active right now" — which is correct only when the assignment belongs to the guest whose
        /// dialogue is currently running. If a phone call interrupts a counter check-in and the
        /// player assigns a room to that (now-suspended) counter guest mid-call, the old code would
        /// enable the choice on the phone Bubble instead of the counter Bubble that actually owns it.
        /// Passing the guest explicitly lets this method check identity instead of guessing from a
        /// single global flag.
        /// </summary>
        public void NotifyRoomAssigned(Animal guest)
        {
            if (guest == null) return;

            if (guest == CurrentGuest)
            {
                // This guest owns whichever dialogue is currently running (counter or phone) —
                // ActiveStaffBubble already resolves to the right instance for that active context.
                _roomAssigned = true;
                ActiveStaffBubble?.EnableAssignChoices();
            }
            else if (_hasSavedCheckIn && guest == _savedGuest)
            {
                // This guest's counter dialogue is suspended behind an interrupting phone call right
                // now — its Bubble is hidden/paused, so there's nothing to enable on screen yet. Just
                // remember the flag; RunPhoneCallDialogue's resume path folds _savedRoomAssigned back
                // into _roomAssigned and rebuilds the choice buttons from that value once the counter
                // dialogue actually resumes.
                _savedRoomAssigned = true;
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] NotifyRoomAssigned({guest.guestName}) doesn't match the " +
                    $"active ({CurrentGuest?.guestName}) or suspended ({_savedGuest?.guestName}) dialogue guest — ignored.");
            }
        }

        public void StartDialogue(Animal guest, bool claimsReservation)
        {
            // A phone call is currently occupying the dialogue engine. There's no in-progress counter
            // dialogue to interrupt/resume here — this is a brand-new guest whose StartDialogue call
            // simply arrived at a bad time. Previously this fell through to `if (_isRunning) return;`
            // and was silently dropped forever (CounterFlow would then hang on WaitUntil(_dialogueFinished)
            // with the guest's SpeechBubble never turning on). Defer it instead: RunPhoneCallDialogue
            // will call StartDialogue again for this guest once the call actually ends.
            if (_isPhoneCall)
            {
                _hasPendingStart = true;
                _pendingGuest = guest;
                _pendingClaimsReservation = claimsReservation;
                Debug.Log($"[DialogueManager] 전화 응대 중이라 {guest.guestName}의 대화 시작을 통화 종료 후로 미룹니다.");
                return;
            }

            if (_isRunning) return;
            _hasSavedCheckIn = false;
            _savedGuest = null;
            _savedNodes = null;
            _savedCurrentNode = null;
            _savedRoomAssigned = false;
            _hasPendingStart = false;
            _pendingGuest = null;
            _isPhoneCall = false;

            CurrentGuest = guest;
            _nodes = DialogueTreeBuilder.Build(
                guest.guestName,
                guest.species.displayName,
                guest.hasReservation,
                claimsReservation,
                guest.species);
            _roomAssigned = false;
            StartCoroutine(RunDialogue());
        }

        public void StartPhoneCallDialogue(Animal guest, int roomNumber)
        {
            if (_isRunning && !_isPhoneCall)
            {
                _hasSavedCheckIn = true;
                _savedGuest = CurrentGuest;
                _savedNodes = _nodes;
                _savedCurrentNode = _currentNode;
                _savedRoomAssigned = _roomAssigned;
            }

            if (_isRunning)
            {
                StopAllCoroutines();
                // 카운터 Bubble은 절대 숨기지 않는다 — 인터럽트 당한 대화의 마지막 상태가 그대로
                // 화면에 남아있어야 전화 Bubble이 그 위를 "덮는" 느낌을 낼 수 있다. 여기서 숨기는 건
                // 혹시 이전 통화의 전화 Bubble이 정리되지 않고 남아있을 경우에 대한 안전장치뿐이다.
                HidePhoneBubbles();
                _isRunning = false;
            }

            _isPhoneCall = true;
            CurrentGuest = guest;

            // Which opening line fits depends on what's actually bothering the caller (a floor-to-floor
            // "누가 위에서 쿵쿵대요" complaint vs. a same-floor wall "옆방이 시끄러워요" complaint) — not
            // on the caller's OWN nuisance-causing traits, which describe what THEY'D do to a neighbor,
            // not what's happening to them. See RoomManager.IsRoomSufferingFloorNuisance.
            if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
            bool isFloorNuisance = roomManager != null && roomManager.IsRoomSufferingFloorNuisance(roomNumber);
            _nodes = DialogueTreeBuilder.BuildPhoneCallTree(guest, roomNumber, isFloorNuisance);
            _roomAssigned = false;
            StartCoroutine(RunPhoneCallDialogue());
        }

        private IEnumerator RunDialogue(bool resumeFromCurrent = false)
        {
            _isRunning = true;
            _isPhoneCall = false;
            if (!resumeFromCurrent)
            {
                HideCounterBubbles();
                _currentNode = GetNode("start");
            }

            while (_currentNode != null)
            {
                yield return ProcessNode(_currentNode);
                string nextId = ResolveNextNode(_currentNode);
                if (string.IsNullOrEmpty(nextId)) break;
                _currentNode = GetNode(nextId);
                if (delayBetweenNodes > 0f) yield return new WaitForSeconds(delayBetweenNodes);
            }
            HideCounterBubbles();
            _isRunning = false;
            string exitId = _currentNode != null ? _currentNode.id : "exit_leave";
            OnDialogueEnd?.Invoke(exitId);
        }

        private IEnumerator RunPhoneCallDialogue()
        {
            _isRunning = true;
            HidePhoneBubbles();
            _currentNode = GetNode("start");
            Animal callGuest = CurrentGuest;
            while (_currentNode != null)
            {
                yield return ProcessNode(_currentNode);
                string nextId = ResolveNextNode(_currentNode);
                if (string.IsNullOrEmpty(nextId)) break;
                _currentNode = GetNode(nextId);
                if (delayBetweenNodes > 0f) yield return new WaitForSeconds(delayBetweenNodes);
            }
            // 전화 Bubble만 정리한다. 카운터 Bubble은 인터럽트 내내 숨겨진 적이 없으므로
            // (또는 애초에 전화가 아니었으므로) 여기서 손댈 필요가 없다 — 그대로 드러난다.
            HidePhoneBubbles();
            _isRunning = false;
            _isPhoneCall = false;

            string phoneExitId = _currentNode != null ? _currentNode.id : "phone_exit_no_move";
            OnPhoneCallDialogueEnd?.Invoke(callGuest, phoneExitId);

            // OnPhoneCallDialogueEnd is handled synchronously (DayManager -> RoomManager.
            // ResolvePhoneCallDialogue), so by this point RoomManager.GuestAwaitingMove is already
            // set if this call ended with "방을 옮겨드리겠습니다". If so, the complaining guest's
            // room reassignment must finish FIRST — resuming the interrupted check-in dialogue right
            // away would let the player go straight back to the new guest before ever dealing with
            // the promised move. Wait here until the move is resolved (RoomUI.ResolveRoomMove clears
            // GuestAwaitingMove) or was never promised in the first place (condition already true).
            if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
            if (roomManager != null)
            {
                yield return new WaitUntil(() => roomManager.GuestAwaitingMove != callGuest);
            }

            // Even after the reassignment itself is done, the player is still looking at the room
            // panel on the tablet at that exact instant (they just clicked Assign there). Popping the
            // new guest's dialogue back up while that panel is still open would appear behind/under it
            // — wait for the player to actually close the tablet before resuming. When no move was
            // ever promised the tablet generally isn't open for this reason at all, so this is a no-op
            // in that case.
            if (tabletController == null) tabletController = FindFirstObjectByType<TabletController>();
            if (tabletController != null)
            {
                yield return new WaitUntil(() => !tabletController.IsOpen);
            }

            if (_hasSavedCheckIn)
            {
                CurrentGuest = _savedGuest;
                _nodes = _savedNodes;
                _currentNode = _savedCurrentNode;
                _roomAssigned = _savedRoomAssigned || _roomAssigned;

                _hasSavedCheckIn = false;
                _savedGuest = null;
                _savedNodes = null;
                _savedCurrentNode = null;

                StartCoroutine(RunDialogue(resumeFromCurrent: true));
            }
            else if (_hasPendingStart)
            {
                // A new guest's StartDialogue arrived while this call was in progress and was deferred
                // (see StartDialogue). Now that the call has fully ended, honor it as a fresh start —
                // not a resume, since that guest's dialogue never actually began.
                Animal pendingGuest = _pendingGuest;
                bool pendingClaims = _pendingClaimsReservation;
                _hasPendingStart = false;
                _pendingGuest = null;

                StartDialogue(pendingGuest, pendingClaims);
            }
        }

        private IEnumerator ProcessNode(DialogueNode node)
        {
            switch (node.nodeType)
            {
                case NodeType.Line: yield return ProcessLine(node); break;
                case NodeType.Choice: yield return ProcessChoice(node); break;
                case NodeType.TabletCheck: yield return ProcessTabletCheck(node); break;
                case NodeType.CustomerReaction: yield return ProcessCustomerReaction(node); break;
                case NodeType.Exit: yield return ProcessExit(node); break;
            }
        }

        // 문제3 수정: 상대방 말풍선 안 끔! 둘 다 동시에 보일 수 있음
        private IEnumerator ProcessLine(DialogueNode node)
        {
            if (node.speaker == Speaker.Customer)
            {
                if (ActiveCustomerBubble != null)
                {
                    yield return ActiveCustomerBubble.ShowWithText(node.text);
                    yield return new WaitForSeconds(delayAfterLine);
                }
            }
            else
            {
                if (ActiveStaffBubble != null)
                {
                    yield return ActiveStaffBubble.ShowLine(node.text);
                    yield return new WaitForSeconds(delayAfterLine);
                }
            }
        }

        private IEnumerator ProcessChoice(DialogueNode node)
        {
            var staff = ActiveStaffBubble;
            if (staff == null || node.choices == null || node.choices.Count == 0) yield break;
            // 손님 말풍선은 그대로 두고 직원 선택지만 표시
            string lineText = node.text != null ? node.text : "";
            var options = new List<string>();
            var optionStates = new List<bool>();
            foreach (var choice in node.choices)
            {
                options.Add(choice.text);
                bool isAssignChoice = choice.text.Contains("방 배정") || choice.text.Contains("배정해");
                bool isEnabled = !isAssignChoice || _roomAssigned;
                optionStates.Add(isEnabled);
            }
            yield return staff.ShowLineWithChoices(lineText, options, optionStates);
        }

        private IEnumerator ProcessTabletCheck(DialogueNode node)
        {
            // 손님 말풍선 그대로 유지, 직원 대사만 표시
            if (ActiveStaffBubble != null) yield return ActiveStaffBubble.ShowLine(node.text);
            yield return new WaitForSeconds(tabletCheckMinDelay);
            if (tabletController != null && tabletController.IsOpen)
            {
                yield return new WaitUntil(() => !tabletController.IsOpen);
                yield return new WaitForSeconds(0.3f);
            }
        }

        private IEnumerator ProcessCustomerReaction(DialogueNode node)
        {
            bool begs = UnityEngine.Random.value < node.begChance;
            node.nextNodeId = begs ? node.begNodeId : node.leaveNodeId;
            yield return null;
        }

        private IEnumerator ProcessExit(DialogueNode node)
        {
            if (ActiveStaffBubble != null) ActiveStaffBubble.HideImmediate();
            if (ActiveCustomerBubble != null && !string.IsNullOrEmpty(node.text))
            {
                yield return ActiveCustomerBubble.ShowWithText(node.text);
                yield return new WaitForSeconds(delayAfterLine);
            }
        }

        private string ResolveNextNode(DialogueNode node)
        {
            var staff = ActiveStaffBubble;
            if (node.nodeType == NodeType.Choice && node.choices != null && staff != null)
            {
                int idx = staff.SelectedIndex;
                if (idx >= 0 && idx < node.choices.Count)
                {
                    string nextNodeId = node.choices[idx].nextNodeId;
                    OnChoiceResolved?.Invoke(nextNodeId);
                    return nextNodeId;
                }
            }
            if (node.nodeType == NodeType.Exit) return null;
            return node.nextNodeId;
        }

        private DialogueNode GetNode(string id)
        {
            if (_nodes != null && _nodes.TryGetValue(id, out var node)) return node;
            Debug.LogWarning("[DialogueManager] 노드를 찾을 수 없음: " + id);
            return null;
        }

        /// <summary>카운터 대화용 Bubble만 숨긴다. 전화 Bubble에는 영향 없음.</summary>
        private void HideCounterBubbles()
        {
            if (customerBubble != null) customerBubble.HideImmediate();
            if (staffBubble != null) staffBubble.HideImmediate();
        }

        /// <summary>전화 대화용 Bubble만 숨긴다. 카운터 Bubble에는 영향 없음 —
        /// 인터럽트 중에도 카운터 대화의 마지막 상태가 화면에 그대로 남아있어야
        /// 전화 Bubble이 그 위를 "덮는" 느낌을 낼 수 있다.</summary>
        private void HidePhoneBubbles()
        {
            if (phoneCustomerBubble != null) phoneCustomerBubble.HideImmediate();
            if (phoneStaffBubble != null) phoneStaffBubble.HideImmediate();
        }
    }
}
