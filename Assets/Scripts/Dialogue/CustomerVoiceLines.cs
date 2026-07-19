using System.Collections.Generic;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 종(speciesId)별 손님 대사 세트.
    /// 친구 코드(SpeciesData)는 건드리지 않고, speciesId만 읽어서 여기서 말투를 결정한다.
    /// 매칭되는 종이 없으면 기본 대사로 폴백.
    /// </summary>
    public static class CustomerVoiceLines
    {
        /// <summary>손님 대사 상황 키.</summary>
        public enum Line
        {
            ClaimReservation,   // "예약했어요"
            TellInfo,           // 이름/종 알려줌 (템플릿: {0}=이름, {1}=종)
            ReactDeniedAngry,   // 실제 예약인데 거부당해 화남
            ExitCheckinAngry,   // 화난 채 체크인 후 퇴장
            ExitRejectedAngry,  // 화난 채 거절당해 퇴장
            Beg,                // 빌기 (예약 주장 분기)
            NoReservation,      // "예약 안 했어요"
            BegWalkIn,          // 빌기 (워크인 분기)
            ExitCheckin,        // 체크인 감사 퇴장
            ExitLeave,          // 빌다 거절당해 떠남
            ExitRejected,       // 거절 수긍 퇴장
            ExitRejectedNoRoom  // 예약했지만 빈 방이 없어 거절당함
        }

        private class VoiceSet
        {
            public Dictionary<Line, string> lines = new Dictionary<Line, string>();
        }

        private static readonly Dictionary<string, VoiceSet> _byId =
            new Dictionary<string, VoiceSet>(System.StringComparer.OrdinalIgnoreCase);

        // 기본 대사 (종 매칭 실패 시 폴백) — 기존 DialogueTreeBuilder 문구와 동일
        private static readonly Dictionary<Line, string> _default = new Dictionary<Line, string>
        {
            { Line.ClaimReservation,  "네, 예약했어요." },
            { Line.TellInfo,          "{0}, {1}입니다." },
            { Line.ReactDeniedAngry,  "예약했는데 이게 무슨 말이에요?!" },
            { Line.ExitCheckinAngry,  "...다음부턴 똑바로 해주세요." },
            { Line.ExitRejectedAngry, "말도 안 돼... 다시는 안 올 겁니다!" },
            { Line.Beg,               "제발요... 오늘 갈 데가 없어요." },
            { Line.NoReservation,     "아뇨, 예약은 안 했어요." },
            { Line.BegWalkIn,         "제발요... 하룻밤만요." },
            { Line.ExitCheckin,       "감사합니다!" },
            { Line.ExitLeave,         "알겠습니다..." },
            { Line.ExitRejected,      "네... 알겠습니다." },
            { Line.ExitRejectedNoRoom,"빈 방이 없다니요...?\n예약했는데... 알겠습니다." },
        };

        static CustomerVoiceLines()
        {
            // 다람쥐 — 소심·수다, 오독오도ㄱ
            Register("squirrel", new Dictionary<Line, string>
            {
                { Line.ClaimReservation,  "네! 예약했어요, 오독오독." },
                { Line.TellInfo,          "{0}, {1}이에요! 오독오독." },
                { Line.ReactDeniedAngry,  "예, 예약했는데요?! 오독... 어떻게 이러실 수가 있어요!" },
                { Line.ExitCheckinAngry,  "...다음엔 꼭 확인해 주세요. 오독오독." },
                { Line.ExitRejectedAngry, "너무해요... 흑, 오독오독... 다신 안 올 거예요!" },
                { Line.Beg,               "제, 제발요... 오늘 갈 데가 없어요, 오독오독..." },
                { Line.NoReservation,     "아, 아뇨... 예약은 못 했어요. 오독오독." },
                { Line.BegWalkIn,         "하, 하룻밤만요! 제발... 오독오독." },
                { Line.ExitCheckin,       "감사합니다! 오독오독!" },
                { Line.ExitLeave,         "아... 네, 알겠어요. 오독..." },
                { Line.ExitRejected,      "네... 알겠습니다. 오독오독." },
                { Line.ExitRejectedNoRoom,"비, 빈 방이 없다구요...?\n예약했는데... 오독..." },
            });

            // 쥐 — 약삭빠르고 까칠, 찍찍
            Register("mouse", new Dictionary<Line, string>
            {
                { Line.ClaimReservation,  "예약했지, 찍찍. 빨리 좀 해줘요." },
                { Line.TellInfo,          "{0}, {1}이야. 찍찍~" },
                { Line.ReactDeniedAngry,  "찍찍?! 예약했다니까 그러네, 똑바로 봐요!" },
                { Line.ExitCheckinAngry,  "흥, 찍찍. 다음부턴 잘 좀 하라고." },
                { Line.ExitRejectedAngry, "말도 안 돼! 찍찍찍! 다신 이딴 데 안 와!" },
                { Line.Beg,               "아 좀... 찍찍, 갈 데가 없단 말이야." },
                { Line.NoReservation,     "예약? 안 했는데. 찍찍." },
                { Line.BegWalkIn,         "하룻밤만, 찍찍. 응? 좀 봐줘요." },
                { Line.ExitCheckin,       "어, 고맙수다. 찍찍~" },
                { Line.ExitLeave,         "쳇, 알았어. 찍찍." },
                { Line.ExitRejected,      "흥, 알겠다고. 찍찍." },
                { Line.ExitRejectedNoRoom,"빈 방이 없다고? 찍찍...\n예약까지 했는데 이게 뭐야." },
            });

            // 토끼 — 예의 바르고 새침, 쿵쿵
            Register("rabbit", new Dictionary<Line, string>
            {
                { Line.ClaimReservation,  "네, 예약했습니다. (쿵쿵)" },
                { Line.TellInfo,          "{0}, {1}입니다. 쿵." },
                { Line.ReactDeniedAngry,  "예약했는데요?! 쿵쿵쿵! 다시 확인해 주세요!" },
                { Line.ExitCheckinAngry,  "...다음부턴 정확히 해주세요. 쿵." },
                { Line.ExitRejectedAngry, "이건 정말 너무하네요! 쿵쿵쿵! 다신 안 옵니다!" },
                { Line.Beg,               "부탁드려요... 오늘 정말 갈 곳이 없어서요. (쿵...)" },
                { Line.NoReservation,     "아니요, 예약은 하지 못했어요. 쿵." },
                { Line.BegWalkIn,         "하룻밤만 부탁드릴게요... 쿵쿵." },
                { Line.ExitCheckin,       "감사합니다! 쿵쿵!" },
                { Line.ExitLeave,         "네... 알겠습니다. 쿵." },
                { Line.ExitRejected,      "네, 알겠어요... 쿵." },
                { Line.ExitRejectedNoRoom,"빈 방이 없다니요...?\n예약했는데... 쿵..." },
            });

            // 고라니 — 무뚝뚝한데 잘 놀람, 으악~
            Register("roe_deer", new Dictionary<Line, string>
            {
                { Line.ClaimReservation,  "예약했음. ...으악!" },
                { Line.TellInfo,          "{0}, {1}임." },
                { Line.ReactDeniedAngry,  "으아악?! 예약했다고! 뭐가 문제야!" },
                { Line.ExitCheckinAngry,  "...담부턴 똑바로 해. 으악, 깜짝이야." },
                { Line.ExitRejectedAngry, "으아아악! 말도 안 돼! 다신 안 온다!" },
                { Line.Beg,               "갈 데 없음... 으악, 제발 좀." },
                { Line.NoReservation,     "예약? 안 했는데. ...왜." },
                { Line.BegWalkIn,         "하룻밤만. ...으악, 부탁 좀 하자." },
                { Line.ExitCheckin,       "오, 땡큐. ...으악!" },
                { Line.ExitLeave,         "알겠음... 으악." },
                { Line.ExitRejected,      "...그래, 알겠어. 으악." },
                { Line.ExitRejectedNoRoom,"빈 방이 없다고?\n예약했는데. ...으악, 됐다." },
            });
        }

        private static void Register(string speciesId, Dictionary<Line, string> lines)
        {
            _byId[speciesId] = new VoiceSet { lines = lines };
        }

        /// <summary>해당 종의 대사를 반환. 없으면 기본 대사로 폴백.</summary>
        public static string Get(string speciesId, Line line)
        {
            if (!string.IsNullOrEmpty(speciesId)
                && _byId.TryGetValue(speciesId, out var set)
                && set.lines.TryGetValue(line, out var text))
                return text;

            return _default.TryGetValue(line, out var def) ? def : "";
        }
    }
}
