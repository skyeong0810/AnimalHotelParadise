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

        [Header("Tablet")]
        [SerializeField] private TabletController tabletController;

        [Header("Timing")]
        [SerializeField] private float delayBetweenNodes = 0.3f;
        [SerializeField] private float delayAfterLine = 1.2f;
        [SerializeField] private float tabletCheckMinDelay = 0.5f;

        public event Action<string> OnDialogueEnd;
        public event Action<string> OnChoiceResolved;
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

        public void NotifyRoomAssigned()
        {
            _roomAssigned = true;
            if (_hasSavedCheckIn)
            {
                _savedRoomAssigned = true;
            }
            if (staffBubble != null)
            {
                staffBubble.EnableAssignChoices();
            }
        }

        public void StartDialogue(Animal guest, bool claimsReservation)
        {
            if (_isRunning) return;
            _hasSavedCheckIn = false;
            _savedGuest = null;
            _savedNodes = null;
            _savedCurrentNode = null;
            _savedRoomAssigned = false;
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
                HideAllBubbles();
                _isRunning = false;
            }

            _isPhoneCall = true;
            CurrentGuest = guest;
            _nodes = DialogueTreeBuilder.BuildPhoneCallTree(guest, roomNumber);
            _roomAssigned = false;
            StartCoroutine(RunPhoneCallDialogue());
        }

        private IEnumerator RunDialogue(bool resumeFromCurrent = false)
        {
            _isRunning = true;
            _isPhoneCall = false;
            if (!resumeFromCurrent)
            {
                HideAllBubbles();
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
            HideAllBubbles();
            _isRunning = false;
            string exitId = _currentNode != null ? _currentNode.id : "exit_leave";
            OnDialogueEnd?.Invoke(exitId);
        }

        private IEnumerator RunPhoneCallDialogue()
        {
            _isRunning = true;
            HideAllBubbles();
            _currentNode = GetNode("start");
            while (_currentNode != null)
            {
                yield return ProcessNode(_currentNode);
                string nextId = ResolveNextNode(_currentNode);
                if (string.IsNullOrEmpty(nextId)) break;
                _currentNode = GetNode(nextId);
                if (delayBetweenNodes > 0f) yield return new WaitForSeconds(delayBetweenNodes);
            }
            HideAllBubbles();
            _isRunning = false;
            _isPhoneCall = false;

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
                if (customerBubble != null)
                {
                    yield return customerBubble.ShowWithText(node.text);
                    yield return new WaitForSeconds(delayAfterLine);
                }
            }
            else
            {
                if (staffBubble != null)
                {
                    yield return staffBubble.ShowLine(node.text);
                    yield return new WaitForSeconds(delayAfterLine);
                }
            }
        }

        private IEnumerator ProcessChoice(DialogueNode node)
        {
            if (staffBubble == null || node.choices == null || node.choices.Count == 0) yield break;
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
            yield return staffBubble.ShowLineWithChoices(lineText, options, optionStates);
        }

        private IEnumerator ProcessTabletCheck(DialogueNode node)
        {
            // 손님 말풍선 그대로 유지, 직원 대사만 표시
            if (staffBubble != null) yield return staffBubble.ShowLine(node.text);
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
            if (staffBubble != null) staffBubble.HideImmediate();
            if (customerBubble != null && !string.IsNullOrEmpty(node.text))
            {
                yield return customerBubble.ShowWithText(node.text);
                yield return new WaitForSeconds(delayAfterLine);
            }
        }

        private string ResolveNextNode(DialogueNode node)
        {
            if (node.nodeType == NodeType.Choice && node.choices != null && staffBubble != null)
            {
                int idx = staffBubble.SelectedIndex;
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

        private void HideAllBubbles()
        {
            if (customerBubble != null) customerBubble.HideImmediate();
            if (staffBubble != null) staffBubble.HideImmediate();
        }
    }
}
