using System.Collections.Generic;

namespace AnimalHotel.Counter
{
    /// <summary>
    /// 손님 대사의 기본 문장과 종별 말버릇을 조합한다.
    /// 말버릇은 SpeciesDatabase의 각 SpeciesData에서 수정한다.
    /// </summary>
    public static class CustomerVoiceLines
    {
        /// <summary>손님 대사 상황 키.</summary>
        public enum Line
        {
            ClaimReservation,
            TellInfo,
            ReactDeniedAngry,
            ExitCheckinAngry,
            ExitRejectedAngry,
            Beg,
            NoReservation,
            BegWalkIn,
            ExitCheckin,
            ExitLeave,
            ExitRejected,
            ExitRejectedNoRoom
        }

        private static readonly Dictionary<Line, string> DefaultLines = new Dictionary<Line, string>
        {
            { Line.ClaimReservation,   "네, 예약했어요." },
            { Line.TellInfo,           "{0}, {1}입니다." },
            { Line.ReactDeniedAngry,   "예약했는데 이게 무슨 말이에요?!" },
            { Line.ExitCheckinAngry,   "...다음부턴 똑바로 해주세요." },
            { Line.ExitRejectedAngry,  "말도 안 돼... 다시는 안 올 겁니다!" },
            { Line.Beg,                "제발요... 오늘 갈 데가 없어요." },
            { Line.NoReservation,      "아뇨, 예약은 안 했어요." },
            { Line.BegWalkIn,          "제발요... 하룻밤만요." },
            { Line.ExitCheckin,        "감사합니다!" },
            { Line.ExitLeave,          "알겠습니다..." },
            { Line.ExitRejected,       "네... 알겠습니다." },
            { Line.ExitRejectedNoRoom, "빈 방이 없다니요...?\n예약했는데... 알겠습니다." }
        };

        /// <summary>종별 상황 대사가 있으면 사용하고, 없으면 기본 대사를 사용한 뒤 말버릇을 붙인다.</summary>
        public static string Get(SpeciesData species, Line line)
        {
            string text = GetSpeciesOverride(species, line);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = DefaultLines.TryGetValue(line, out string value) ? value : string.Empty;
            }

            return AppendSpeechHabit(text, species != null ? species.speechHabit : null);
        }

        private static string AppendSpeechHabit(string text, string speechHabit)
        {
            if (string.IsNullOrWhiteSpace(speechHabit)) return text;

            string habit = speechHabit.Trim();
            char lastCharacter = habit[habit.Length - 1];
            bool hasPunctuation = lastCharacter == '.'
                || lastCharacter == '!'
                || lastCharacter == '?'
                || lastCharacter == '~'
                || lastCharacter == '…';

            if (!hasPunctuation) habit += ".";
            return string.IsNullOrEmpty(text) ? habit : text + " " + habit;
        }

        private static string GetSpeciesOverride(SpeciesData species, Line line)
        {
            SpeciesDialogueLines lines = species != null ? species.dialogueLines : null;
            if (lines == null) return null;

            string text;
            switch (line)
            {
                case Line.ClaimReservation:
                    text = lines.claimReservation;
                    break;
                case Line.TellInfo:
                    text = lines.tellInfo;
                    break;
                case Line.ReactDeniedAngry:
                    text = lines.reactDeniedAngry;
                    break;
                case Line.ExitCheckinAngry:
                    text = lines.exitCheckinAngry;
                    break;
                case Line.ExitRejectedAngry:
                    text = lines.exitRejectedAngry;
                    break;
                case Line.Beg:
                    text = lines.beg;
                    break;
                case Line.NoReservation:
                    text = lines.noReservation;
                    break;
                case Line.BegWalkIn:
                    text = lines.begWalkIn;
                    break;
                case Line.ExitCheckin:
                    text = lines.exitCheckin;
                    break;
                case Line.ExitLeave:
                    text = lines.exitLeave;
                    break;
                case Line.ExitRejected:
                    text = lines.exitRejected;
                    break;
                case Line.ExitRejectedNoRoom:
                    text = lines.exitRejectedNoRoom;
                    break;
                default:
                    text = null;
                    break;
            }

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
}
}
