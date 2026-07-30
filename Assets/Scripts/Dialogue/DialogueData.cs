using System;
using System.Collections.Generic;

namespace AnimalHotel.Counter
{
    /// <summary>말하는 주체</summary>
    public enum Speaker { Staff, Customer }

    /// <summary>대화 노드 종류</summary>
    public enum NodeType
    {
        Line,              // 대사만 (자동 진행)
        Choice,            // 플레이어 선택지
        TabletCheck,       // 대사 + 태블릿 열 수 있는 구간
        CustomerReaction,  // 손님 랜덤 반응 (빌기 or 퇴장)
        Exit               // 손님 퇴장
    }

    [Serializable]
    public class DialogueNode
    {
        public string id;
        public NodeType nodeType;
        public Speaker speaker;
        public string text;
        public List<DialogueChoice> choices;
        public string nextNodeId;

        // CustomerReaction 전용
        public float begChance = 0.5f;
        public string begNodeId;
        public string leaveNodeId;

        // TODO: 평점 시스템 연동용. 이 노드를 거치면 평점에 영향을 줌.
        public int reputationDelta = 0;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string text;
        public string nextNodeId;
    }

    /// <summary>
    /// 대화 트리 빌더.
    /// 손님 예약 여부, 주장 여부에 따라 분기되는 대화 트리를 생성한다.
    /// </summary>
    public static class DialogueTreeBuilder
    {
        public static Dictionary<string, DialogueNode> Build(
            string guestName, string speciesName,
            bool hasReservation, bool claimsReservation,
            SpeciesData species = null)
        {
            var nodes = new Dictionary<string, DialogueNode>();

            // ── 인사 ──
            nodes["start"] = new DialogueNode
            {
                id = "start",
                nodeType = NodeType.Line,
                speaker = Speaker.Staff,
                text = "어서오세요, 예약하셨나요?",
                nextNodeId = claimsReservation ? "customer_yes" : "customer_no"
            };

            if (claimsReservation)
                BuildReservationBranch(nodes, guestName, speciesName, hasReservation, species);
            else
                BuildWalkInBranch(nodes, guestName, speciesName, species);

            AddExitNodes(nodes, species);
            return nodes;
        }

        public static Dictionary<string, DialogueNode> BuildPhoneCallTree(Animal sufferingGuest, int roomNumber)
        {
            var nodes = new Dictionary<string, DialogueNode>();

            string openingText = (sufferingGuest != null && sufferingGuest.willCauseFloorNuisance)
                ? "여보세요? 윗방에서 엄청 쿵쿵대서 미칠 것 같아요!"
                : "아아!! 누가 계속 소리를 질러서 잠을 못 자겠어요!";

            nodes["start"] = new DialogueNode
            {
                id = "start",
                nodeType = NodeType.Line,
                speaker = Speaker.Customer,
                text = openingText,
                nextNodeId = "staff_ask_room"
            };

            nodes["staff_ask_room"] = new DialogueNode
            {
                id = "staff_ask_room",
                nodeType = NodeType.Line,
                speaker = Speaker.Staff,
                text = "죄송합니다. 몇 호에 계신가요?",
                nextNodeId = "customer_tell_room"
            };

            nodes["customer_tell_room"] = new DialogueNode
            {
                id = "customer_tell_room",
                nodeType = NodeType.Line,
                speaker = Speaker.Customer,
                text = $"{roomNumber}호예요.",
                nextNodeId = "staff_phone_choice"
            };

            nodes["staff_phone_choice"] = new DialogueNode
            {
                id = "staff_phone_choice",
                nodeType = NodeType.Choice,
                speaker = Speaker.Staff,
                text = "",
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice
                    {
                        text = "빈 방으로 옮겨 드릴게요. 다시 한 번 죄송합니다.",
                        nextNodeId = "phone_exit_move"
                    },
                    new DialogueChoice
                    {
                        text = "정말 죄송합니다. 방을 옮겨드리고 싶은데, 조건에 맞는 방이 없어요.",
                        nextNodeId = "phone_exit_no_move"
                    }
                }
            };

            nodes["phone_exit_move"] = new DialogueNode
            {
                id = "phone_exit_move",
                nodeType = NodeType.Exit,
                text = ""
            };

            nodes["phone_exit_no_move"] = new DialogueNode
            {
                id = "phone_exit_no_move",
                nodeType = NodeType.Exit,
                text = ""
            };

            return nodes;
        }

        // ────────────────────────────────────────
        //  YES 분기: 손님이 예약했다고 주장
        // ────────────────────────────────────────
        private static void BuildReservationBranch(
            Dictionary<string, DialogueNode> nodes,
            string guestName, string speciesName, bool hasReservation, SpeciesData species)
        {
            // 손님: 예약했다고 답변
            nodes["customer_yes"] = new DialogueNode
            {
                id = "customer_yes",
                nodeType = NodeType.Line,
                speaker = Speaker.Customer,
                text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ClaimReservation),
                nextNodeId = "staff_check_reservation"
            };

            // 직원: 태블릿 확인 구간
            nodes["staff_check_reservation"] = new DialogueNode
            {
                id = "staff_check_reservation",
                nodeType = NodeType.TabletCheck,
                speaker = Speaker.Staff,
                text = "잠시만요, 확인해드릴게요.",
                nextNodeId = "staff_reservation_choices"
            };

            // 직원 선택지 3개
            nodes["staff_reservation_choices"] = new DialogueNode
            {
                id = "staff_reservation_choices",
                nodeType = NodeType.Choice,
                speaker = Speaker.Staff,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { text = "네, 방 배정해드렸어요.", nextNodeId = "exit_checkin" },
                    new DialogueChoice { text = "죄송하지만, 빈 방이 없습니다.", nextNodeId = "exit_rejected_no_room" },
                    new DialogueChoice { text = "죄송하지만, 예약 정보가 확인되지 않네요.", nextNodeId = "customer_react_denied" },
                    new DialogueChoice { text = "예약하신 분 성함과 종을 알려주시겠어요?", nextNodeId = "customer_tell_info" }
                }
            };

            // 손님이 이름/종 알려줌
            nodes["customer_tell_info"] = new DialogueNode
            {
                id = "customer_tell_info",
                nodeType = NodeType.Line,
                speaker = Speaker.Customer,
                text = string.Format(CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.TellInfo), guestName, speciesName),
                nextNodeId = "staff_confirm_info"
            };

            nodes["staff_confirm_info"] = new DialogueNode
            {
                id = "staff_confirm_info",
                nodeType = NodeType.Choice,
                speaker = Speaker.Staff,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { text = "잠시만요, 방 배정해드렸어요.", nextNodeId = "exit_checkin" },
                    new DialogueChoice { text = "죄송하지만, 빈 방이 없습니다.", nextNodeId = "exit_rejected_no_room" }
                }
            };

            // ── 예약 거부 시 반응: 실제 예약 여부에 따라 다름 ──
            if (hasReservation)
            {
                // 실제 예약자인데 거부당함 → 화남 (평점 감소 예정)
                nodes["customer_react_denied"] = new DialogueNode
                {
                    id = "customer_react_denied",
                    nodeType = NodeType.Line,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ReactDeniedAngry),
                    nextNodeId = "staff_apologize",
                    reputationDelta = -1  // TODO: 평점 시스템 연동
                };

                // 직원: 사과
                nodes["staff_apologize"] = new DialogueNode
                {
                    id = "staff_apologize",
                    nodeType = NodeType.Line,
                    speaker = Speaker.Staff,
                    text = "죄송합니다, 다시 한번 확인해볼게요.",
                    nextNodeId = "staff_re_check_choices"
                };

                // 직원: 재확인 후 선택지
                nodes["staff_re_check_choices"] = new DialogueNode
                {
                    id = "staff_re_check_choices",
                    nodeType = NodeType.Choice,
                    speaker = Speaker.Staff,
                    choices = new List<DialogueChoice>
                    {
                        new DialogueChoice { text = "확인했습니다, 방 배정해드릴게요.", nextNodeId = "exit_checkin_angry" },
                        new DialogueChoice { text = "죄송하지만, 빈 방이 없습니다.", nextNodeId = "exit_rejected_no_room" },
                        new DialogueChoice { text = "정말 죄송합니다, 확인이 어렵습니다.", nextNodeId = "exit_rejected_angry" }
                    }
                };

                // 화난 상태에서 체크인 → 불만족 퇴장
                nodes["exit_checkin_angry"] = new DialogueNode
                {
                    id = "exit_checkin_angry",
                    nodeType = NodeType.Exit,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ExitCheckinAngry),
                    reputationDelta = -1  // TODO: 평점 시스템 연동
                };

                // 화난 상태에서 거절 → 매우 불만족 퇴장
                nodes["exit_rejected_angry"] = new DialogueNode
                {
                    id = "exit_rejected_angry",
                    nodeType = NodeType.Exit,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ExitRejectedAngry),
                    reputationDelta = -3  // TODO: 평점 시스템 연동
                };
            }
            else
            {
                // 실제로 예약 없는데 있다고 주장 → 기존 로직 (빌거나 퇴장)
                nodes["customer_react_denied"] = new DialogueNode
                {
                    id = "customer_react_denied",
                    nodeType = NodeType.CustomerReaction,
                    speaker = Speaker.Customer,
                    begChance = 0.5f,
                    begNodeId = "customer_beg_reserved",
                    leaveNodeId = "exit_leave"
                };

                nodes["customer_beg_reserved"] = new DialogueNode
                {
                    id = "customer_beg_reserved",
                    nodeType = NodeType.Line,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.Beg),
                    nextNodeId = "staff_hmm_reserved"
                };

                nodes["staff_hmm_reserved"] = new DialogueNode
                {
                    id = "staff_hmm_reserved",
                    nodeType = NodeType.Line,
                    speaker = Speaker.Staff,
                    text = "음....",
                    nextNodeId = "staff_beg_choices_reserved"
                };

                nodes["staff_beg_choices_reserved"] = new DialogueNode
                {
                    id = "staff_beg_choices_reserved",
                    nodeType = NodeType.Choice,
                    speaker = Speaker.Staff,
                    choices = new List<DialogueChoice>
                    {
                        new DialogueChoice { text = "남은 방에 배정해 드렸어요.", nextNodeId = "exit_checkin" },
                        new DialogueChoice { text = "죄송하지만, 다음에 다시 방문해 주세요.", nextNodeId = "exit_rejected" }
                    }
                };
            }
        }

        // ────────────────────────────────────────
        //  NO 분기: 손님이 예약 없다고 말함
        // ────────────────────────────────────────
        private static void BuildWalkInBranch(
            Dictionary<string, DialogueNode> nodes,
            string guestName, string speciesName, SpeciesData species)
        {
            nodes["customer_no"] = new DialogueNode
            {
                id = "customer_no",
                nodeType = NodeType.Line,
                speaker = Speaker.Customer,
                text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.NoReservation),
                nextNodeId = "staff_check_walkin"
            };

            nodes["staff_check_walkin"] = new DialogueNode
            {
                id = "staff_check_walkin",
                nodeType = NodeType.TabletCheck,
                speaker = Speaker.Staff,
                text = "잠시만요, 빈 방이 있는 지 확인해 볼게요.",
                nextNodeId = "staff_walkin_choices"
            };

            nodes["staff_walkin_choices"] = new DialogueNode
            {
                id = "staff_walkin_choices",
                nodeType = NodeType.Choice,
                speaker = Speaker.Staff,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { text = "네, 방 배정해드렸어요.", nextNodeId = "exit_checkin" },
                    new DialogueChoice { text = "죄송하지만, 숙박이 어려울 것 같아요.", nextNodeId = "customer_react_walkin_denied" }
                }
            };

            nodes["customer_react_walkin_denied"] = new DialogueNode
            {
                id = "customer_react_walkin_denied",
                nodeType = NodeType.CustomerReaction,
                speaker = Speaker.Customer,
                begChance = 0.5f,
                begNodeId = "customer_beg_walkin",
                leaveNodeId = "exit_leave"
            };

            nodes["customer_beg_walkin"] = new DialogueNode
            {
                id = "customer_beg_walkin",
                nodeType = NodeType.Line,
                speaker = Speaker.Customer,
                text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.BegWalkIn),
                nextNodeId = "staff_hmm_walkin"
            };

            nodes["staff_hmm_walkin"] = new DialogueNode
            {
                id = "staff_hmm_walkin",
                nodeType = NodeType.Line,
                speaker = Speaker.Staff,
                text = "음....",
                nextNodeId = "staff_beg_choices_walkin"
            };

            nodes["staff_beg_choices_walkin"] = new DialogueNode
            {
                id = "staff_beg_choices_walkin",
                nodeType = NodeType.Choice,
                speaker = Speaker.Staff,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { text = "남은 방에 배정해 드렸어요.", nextNodeId = "exit_checkin" },
                    new DialogueChoice { text = "죄송하지만, 다음에 다시 방문해 주세요.", nextNodeId = "exit_rejected" }
                }
            };
        }

        // ────────────────────────────────────────
        //  공통 퇴장 노드
        // ────────────────────────────────────────
        private static void AddExitNodes(Dictionary<string, DialogueNode> nodes, SpeciesData species)
        {
            if (!nodes.ContainsKey("exit_checkin"))
                nodes["exit_checkin"] = new DialogueNode
                {
                    id = "exit_checkin",
                    nodeType = NodeType.Exit,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ExitCheckin)
                };

            if (!nodes.ContainsKey("exit_leave"))
                nodes["exit_leave"] = new DialogueNode
                {
                    id = "exit_leave",
                    nodeType = NodeType.Exit,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ExitLeave)
                };

            if (!nodes.ContainsKey("exit_rejected"))
                nodes["exit_rejected"] = new DialogueNode
                {
                    id = "exit_rejected",
                    nodeType = NodeType.Exit,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ExitRejected)
                };

            if (!nodes.ContainsKey("exit_rejected_no_room"))
                nodes["exit_rejected_no_room"] = new DialogueNode
                {
                    id = "exit_rejected_no_room",
                    nodeType = NodeType.Exit,
                    speaker = Speaker.Customer,
                    text = CustomerVoiceLines.Get(species, CustomerVoiceLines.Line.ExitRejectedNoRoom)
                };
        }
    }
}
