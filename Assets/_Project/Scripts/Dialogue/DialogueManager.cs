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
        public Animal CurrentGuest { get; private set; }

        private Dictionary<string, DialogueNode> _nodes;
        private DialogueNode _currentNode;
        private bool _isRunning;

        public void StartDialogue(Animal guest, bool claimsReservation)
        {
            if (_isRunning) return;
            CurrentGuest = guest;
            _nodes = DialogueTreeBuilder.Build(guest.guestName, guest.species.displayName, guest.hasReservation, claimsReservation);
            StartCoroutine(RunDialogue());
        }

        private IEnumerator RunDialogue()
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
            string exitId = _currentNode != null ? _currentNode.id : "exit_leave";
            OnDialogueEnd?.Invoke(exitId);
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
            foreach (var choice in node.choices) options.Add(choice.text);
            yield return staffBubble.ShowLineWithChoices(lineText, options);
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
                if (idx >= 0 && idx < node.choices.Count) return node.choices[idx].nextNodeId;
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
