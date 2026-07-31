using System.Collections.Generic;
using UnityEngine;

namespace AnimalHotel.Counter
{
    public enum RoomStatus
    {
        Vacant,
        Occupied,
        NeedsExamination,
        NeedsCleaning,
        AdvancedCleaningInProgress
    }

    public class RoomData
    {
        public int roomNumber;
        public Animal occupant;
        public RoomStatus status;
        public Animal lastOccupant;
        public bool requiresAdvancedCleaning;

        public HashSet<int> incomingNuisanceSources = new HashSet<int>();
        public HashSet<int> outgoingNuisanceTargets = new HashSet<int>();
    }

    public class RoomManager : MonoBehaviour
    {
        public class PendingCall
        {
            public Animal sufferingGuest;
            public int roomNumber;
            public float timer;
            public int lastLoggedSecond = -1;
            public bool isRinging;
        }

        public event System.Action<Animal, int> OnCallRinging;
        public event System.Action<Animal, int, bool> OnCallEnded; // guest, roomNumber, wasAnswered

        public static readonly int RoomCount = 10;

        private RoomData[] _rooms;
        private List<PendingCall> _pendingCalls = new List<PendingCall>();

        /// <summary>
        /// The one call currently being handled (ringing, or answered and mid phone-conversation).
        /// While this is non-null, every other pending call is frozen — no one else's timer ticks
        /// and no one else's phone rings, matching "손님들은 한 번에 한 통화씩만 받는다".
        /// </summary>
        private PendingCall _activeCall;

        /// <summary>
        /// True from the moment a nuisance call starts ringing until its phone dialogue has fully
        /// resolved (see ResolvePhoneCallDialogue). While true, room assignment is locked entirely —
        /// for both the complaining guest (their outcome isn't decided yet) and any new guest who is
        /// mid check-in waiting on their own room. RoomUI checks this before honoring an Assign click.
        /// </summary>
        public bool IsCallActive => _activeCall != null;

        /// <summary>
        /// Guest who was promised a room move during a nuisance phone call ("빈 방으로 옮겨 드릴게요")
        /// but hasn't actually been moved yet. RoomUI checks this first when its Assign button is
        /// clicked, so the same button can complete the promised move instead of check-in assignment.
        /// </summary>
        public Animal GuestAwaitingMove { get; private set; }

        private void Awake()
        {
            _rooms = new RoomData[RoomCount];
            for (int i = 0; i < RoomCount; i++)
            {
                _rooms[i] = new RoomData
                {
                    roomNumber = i + 1,
                    occupant = null,
                    status = RoomStatus.Vacant
                };
            }
        }

        public RoomData GetRoom(int roomNumber) => _rooms[roomNumber - 1];

        /// <summary>Returns the number of rooms that are currently not occupied.</summary>
        public int GetNonOccupiedRoomCount()
        {
            int count = 0;
            if (_rooms != null)
            {
                foreach (var room in _rooms)
                {
                    if (room != null && room.status != RoomStatus.Occupied)
                        count++;
                }
            }
            return count;
        }

        public RoomData GetRoomByOccupant(Animal guest)
        {
            foreach (var room in _rooms)
                if (room.occupant == guest) return room;
            return null;
        }

        public bool AssignRoom(int roomNumber, Animal guest)
        {
            if (guest == null)
            {
                Debug.LogWarning("[RoomManager] Cannot assign a null guest.");
                return false;
            }

            var existingRoom = GetRoomByOccupant(guest);
            if (existingRoom != null)
            {
                Debug.LogWarning($"[RoomManager] {guest.guestName} is already assigned to room {existingRoom.roomNumber}.");
                return false;
            }

            var room = GetRoom(roomNumber);
            if (room.status != RoomStatus.Vacant)
            {
                Debug.LogWarning($"[RoomManager] Room {roomNumber} is not vacant.");
                return false;
            }
            room.occupant = guest;
            room.status = RoomStatus.Occupied;
            room.lastOccupant = null;
            room.requiresAdvancedCleaning = false;

            Debug.Log($"[RoomManager] Room {roomNumber} assigned to {guest.guestName}.");

            // Nuisance is evaluated later, once check-in has fully completed (see EvaluateNuisanceForGuest).
            // This keeps a guest from being flagged as a nuisance source/victim while they're still
            // mid check-in dialogue or walking off-screen.

            return true;
        }

        /// <summary>
        /// Evaluates floor/wall/surround nuisance for a guest who has just finished checking in.
        /// Call this once the check-in flow (dialogue + exit animation) has fully completed —
        /// not at the moment the room is assigned. CounterFlow raises OnGuestSettled for this purpose.
        /// </summary>
        public void EvaluateNuisanceForGuest(Animal guest)
        {
            if (guest == null) return;

            var room = GetRoomByOccupant(guest);
            if (room == null)
            {
                Debug.LogWarning($"[RoomManager] EvaluateNuisanceForGuest: no room found for {guest.guestName}.");
                return;
            }

            EvaluateNuisanceOnAssignment(room);
        }

        /// <summary>
        /// Moves an assigned animal from currentRoomNumber to a vacant newRoomNumber.
        /// Clears outgoing nuisance from the old room and evaluates nuisance in the new room.
        /// </summary>
        public bool MoveAnimal(int currentRoomNumber, int newRoomNumber)
        {
            var currentRoom = GetRoom(currentRoomNumber);
            if (currentRoom == null || currentRoom.occupant == null)
            {
                Debug.LogWarning($"[RoomManager] Room {currentRoomNumber} is empty or invalid.");
                return false;
            }

            var targetRoom = GetRoom(newRoomNumber);
            if (targetRoom == null || targetRoom.status != RoomStatus.Vacant)
            {
                Debug.LogWarning($"[RoomManager] Target room {newRoomNumber} is not vacant.");
                return false;
            }

            Animal guest = currentRoom.occupant;

            // MoveAnimal is only ever called to fulfill a promised nuisance-call room move (see
            // RoomUI.AssignRoomForPendingMove) — this guest just complained and is being relocated
            // because of it. Reset hasCalledNuisance BEFORE evaluating nuisance for the new room below,
            // so that if the new room already has trouble (e.g. an already-occupied noisy neighbor),
            // EvaluateNuisanceOnAssignment's immediate ScheduleNuisanceCall isn't silently swallowed by
            // the old complaint's flag. nuisanceComplaintCount is left untouched — DayManager uses it at
            // checkout to grade "resolved cleanly" apart from "resolved, but it kept happening again".
            guest.hasCalledNuisance = false;

            // Clear outgoing nuisance caused by guest in the current room
            ClearOutgoingNuisance(currentRoom);

            // Vacate current room
            currentRoom.occupant = null;
            currentRoom.lastOccupant = guest;
            currentRoom.requiresAdvancedCleaning = RequiresAdvancedCleaning(guest);
            currentRoom.status = RoomStatus.NeedsExamination;

            // Assign guest to new room
            targetRoom.occupant = guest;
            targetRoom.status = RoomStatus.Occupied;
            targetRoom.lastOccupant = null;
            targetRoom.requiresAdvancedCleaning = false;

            Debug.Log($"[RoomManager] Moved {guest.guestName} from room {currentRoomNumber} to room {newRoomNumber}.");

            // Evaluate nuisance in the new room using guest's saved nuisance flags
            EvaluateNuisanceOnAssignment(targetRoom);

            return true;
        }

        public void VacateRoom(int roomNumber)
        {
            var room = GetRoom(roomNumber);
            var departingGuest = room.occupant;

            ClearOutgoingNuisance(room);

            room.lastOccupant = departingGuest;
            room.requiresAdvancedCleaning = RequiresAdvancedCleaning(departingGuest);
            room.occupant = null;
            room.status = RoomStatus.NeedsExamination;
            Debug.Log($"[RoomManager] Room {roomNumber} vacated - needs examination. Advanced cleaning: {room.requiresAdvancedCleaning}.");

            // If this guest checked out before staff got around to moving them, there's nothing left
            // to move — drop the pending request so RoomUI's Assign button falls back to check-in mode.
            if (GuestAwaitingMove != null && GuestAwaitingMove == departingGuest)
            {
                GuestAwaitingMove = null;
            }
        }

        public bool CleanRoom(int roomNumber)
        {
            var room = GetRoom(roomNumber);
            if (room.status == RoomStatus.NeedsExamination)
            {
                if (room.requiresAdvancedCleaning)
                {
                    room.status = RoomStatus.NeedsCleaning;
                    Debug.Log($"[RoomManager] Room {roomNumber} requires advanced cleaning.");
                    return false;
                }

                MarkRoomVacant(room);
                Debug.Log($"[RoomManager] Room {roomNumber} cleaned - ready for assignment.");
                return true;
            }

            if (room.status == RoomStatus.NeedsCleaning)
            {
                Debug.LogWarning($"[RoomManager] Room {roomNumber} requires advanced cleaning.");
                return false;
            }

            Debug.LogWarning($"[RoomManager] Room {roomNumber} does not need cleaning.");
            return false;
        }

        public bool AdvancedCleanRoom(int roomNumber)
        {
            var room = GetRoom(roomNumber);
            if (room.status == RoomStatus.NeedsExamination || room.status == RoomStatus.NeedsCleaning)
            {
                room.status = RoomStatus.AdvancedCleaningInProgress;
                return true;
            }

            if (room.status == RoomStatus.AdvancedCleaningInProgress)
            {
                return false;
            }
            return false;
        }

        public int CompleteAdvancedCleaningRooms()
        {
            int completedCount = 0;
            foreach (var room in _rooms)
            {
                if (room == null || room.status != RoomStatus.AdvancedCleaningInProgress)
                    continue;

                MarkRoomVacant(room);
                completedCount++;
            }

            return completedCount;
        }


        private void MarkRoomVacant(RoomData room)
        {
            room.occupant = null;
            room.lastOccupant = null;
            room.requiresAdvancedCleaning = false;
            room.status = RoomStatus.Vacant;
        }

        private bool RequiresAdvancedCleaning(Animal guest)
        {
            if (guest == null) return false;
            if (guest.LeavesOdour) return true;
            if (!guest.CausesDamage) return false;

            int damageProbability = Mathf.Clamp(guest.DamageProbability, 0, 100);
            bool causedDamage = Random.Range(0, 100) < damageProbability;
            Debug.Log($"[RoomManager] Damage roll for {guest.guestName}: {damageProbability}% -> {causedDamage}.");
            return causedDamage;
        }

        // ── Nuisance Logic ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the room right below the given room number, or null if none exists.
        /// </summary>
        public RoomData GetRoomBelow(int roomNumber)
        {
            if (roomNumber < 1 || roomNumber > RoomCount) return null;
            int floor = (roomNumber - 1) / 5;
            if (floor == 0) // Top floor (rooms 1..5)
            {
                return GetRoom(roomNumber + 5);
            }
            return null;
        }

        /// <summary>
        /// Returns the room right above the given room number, or null if none exists.
        /// </summary>
        public RoomData GetRoomAbove(int roomNumber)
        {
            if (roomNumber < 1 || roomNumber > RoomCount) return null;
            int floor = (roomNumber - 1) / 5;
            if (floor == 1) // Bottom floor (rooms 6..10)
            {
                return GetRoom(roomNumber - 5);
            }
            return null;
        }

        /// <summary>
        /// Returns adjacent rooms on the same floor (left and right next-door rooms).
        /// </summary>
        public List<RoomData> GetNextRooms(int roomNumber)
        {
            List<RoomData> nextRooms = new List<RoomData>();
            if (roomNumber < 1 || roomNumber > RoomCount) return nextRooms;

            int col = (roomNumber - 1) % 5;
            if (col > 0) // Left neighbor
            {
                nextRooms.Add(GetRoom(roomNumber - 1));
            }
            if (col < 4) // Right neighbor
            {
                nextRooms.Add(GetRoom(roomNumber + 1));
            }
            return nextRooms;
        }

        /// <summary>
        /// Returns all surrounding rooms (top, bottom, left next, right next).
        /// </summary>
        public List<RoomData> GetSurroundRooms(int roomNumber)
        {
            List<RoomData> surroundRooms = new List<RoomData>();
            if (roomNumber < 1 || roomNumber > RoomCount) return surroundRooms;

            var above = GetRoomAbove(roomNumber);
            if (above != null) surroundRooms.Add(above);

            var below = GetRoomBelow(roomNumber);
            if (below != null) surroundRooms.Add(below);

            surroundRooms.AddRange(GetNextRooms(roomNumber));

            return surroundRooms;
        }

        /// <summary>
        /// Whether the nuisance currently bothering <paramref name="roomNumber"/> is coming from a
        /// room directly above or below it (a "층간소음" / floor-to-floor complaint — someone stomping
        /// upstairs) rather than from a same-floor next-door room (a "벽간소음" / wall complaint —
        /// someone shouting through the wall). Determined spatially from
        /// <see cref="RoomData.incomingNuisanceSources"/> rather than from the SUFFERING guest's own
        /// nuisance-causing traits: the guest calling in is the victim, not the source, so their own
        /// willCauseFloorNuisance/willCauseWallNuisance say nothing about what's bothering THEM — only
        /// about what they'd do to a neighbor. Used to pick the right opening line for the phone call
        /// (see DialogueTreeBuilder.BuildPhoneCallTree).
        /// </summary>
        public bool IsRoomSufferingFloorNuisance(int roomNumber)
        {
            var room = GetRoom(roomNumber);
            if (room == null || room.incomingNuisanceSources.Count == 0) return false;

            var above = GetRoomAbove(roomNumber);
            var below = GetRoomBelow(roomNumber);
            return (above != null && room.incomingNuisanceSources.Contains(above.roomNumber))
                || (below != null && room.incomingNuisanceSources.Contains(below.roomNumber));
        }

        private void ClearOutgoingNuisance(RoomData room)
        {
            if (room == null) return;
            foreach (int targetRoomNum in room.outgoingNuisanceTargets)
            {
                var targetRoom = GetRoom(targetRoomNum);
                if (targetRoom != null)
                {
                    targetRoom.incomingNuisanceSources.Remove(room.roomNumber);
                }
            }
            room.outgoingNuisanceTargets.Clear();
        }

        private void EvaluateNuisanceOnAssignment(RoomData sourceRoom)
        {
            Animal guest = sourceRoom.occupant;
            if (guest == null) return;

            // Ensure nuisance determination is made once per guest instance and kept until checkout
            guest.DetermineNuisance();

            bool causedAnyNuisance = false;

            // 1. floorNuisanceProbability cause nuisance to the room right below
            if (guest.willCauseFloorNuisance)
            {
                var target = GetRoomBelow(sourceRoom.roomNumber);
                if (target != null)
                {
                    Debug.Log("will cause floor nuisance");
                    causedAnyNuisance = true;
                    RegisterNuisanceTarget(sourceRoom, target);
                }
            }

            // 2. wallNuisanceProbability cause nuisance to the rooms on the next
            if (guest.willCauseWallNuisance)
            {
                var nextRooms = GetNextRooms(sourceRoom.roomNumber);
                if (nextRooms.Count > 0)
                {
                    Debug.Log("will cause wall nuisance");
                    causedAnyNuisance = true;
                    foreach (var target in nextRooms)
                    {
                        RegisterNuisanceTarget(sourceRoom, target);
                    }
                }
            }

            // 3. surroundNuisanceProbability cause nuisance to the rooms on top, bottom and next
            if (guest.willCauseSurroundNuisance)
            {
                var surroundRooms = GetSurroundRooms(sourceRoom.roomNumber);
                if (surroundRooms.Count > 0)
                {
                    Debug.Log("will cause surround nuisance");
                    causedAnyNuisance = true;
                    foreach (var target in surroundRooms)
                    {
                        RegisterNuisanceTarget(sourceRoom, target);
                    }
                }
            }

            if (!causedAnyNuisance)
            {
                Debug.Log("won't cause nuisance");
            }

            // If THIS newly assigned room is ALREADY affected by nuisance from another occupied room
            if (sourceRoom.incomingNuisanceSources.Count > 0)
            {
                ScheduleNuisanceCall(sourceRoom);
            }
        }

        private void RegisterNuisanceTarget(RoomData sourceRoom, RoomData targetRoom)
        {
            if (sourceRoom == null || targetRoom == null) return;

            sourceRoom.outgoingNuisanceTargets.Add(targetRoom.roomNumber);
            targetRoom.incomingNuisanceSources.Add(sourceRoom.roomNumber);

            // If the room that will get affected is ALREADY occupied, schedule call
            if (targetRoom.status == RoomStatus.Occupied && targetRoom.occupant != null)
            {
                ScheduleNuisanceCall(targetRoom);
            }
        }

        /// <summary>
        /// Evaluates nuisance for all currently occupied rooms that have active incoming nuisance sources.
        /// </summary>
        public void ProcessNuisance()
        {
            if (_rooms == null) return;

            foreach (var room in _rooms)
            {
                if (room != null && room.status == RoomStatus.Occupied && room.occupant != null && room.incomingNuisanceSources.Count > 0)
                {
                    ScheduleNuisanceCall(room);
                }
            }
        }

        private void ScheduleNuisanceCall(RoomData sufferingRoom)
        {
            if (sufferingRoom == null || sufferingRoom.occupant == null) return;
            Animal guest = sufferingRoom.occupant;

            // A guest can't have a second complaint scheduled while one is already outstanding. This
            // flag also stays permanently set for a guest whose complaint went unresolved (no room
            // offered, or the call was missed) — they don't get a second chance. It's only cleared when
            // a promised move actually happens (see MoveAnimal), which is what allows a guest to call
            // again if the *new* room turns out to have its own nuisance problem.
            if (guest.hasCalledNuisance) return;

            foreach (var call in _pendingCalls)
            {
                if (call.sufferingGuest == guest) return;
            }

            // Checkout timing no longer matters here — the day cannot advance to the next time
            // slot while any call is queued, ringing, or being handled (see HasPendingCalls /
            // DayManager.TryAdvancePhase), so every call is guaranteed to happen before anyone
            // involved checks out. The call just needs to land sometime within the current slot.
            float maxDelay = 10f;
            if (DayManager.Instance != null && DayManager.Instance.PhaseTimeRemaining > 0f)
            {
                maxDelay = DayManager.Instance.PhaseTimeRemaining;
            }

            float randomDelay = Random.Range(0f, Mathf.Max(0.1f, maxDelay));

            _pendingCalls.Add(new PendingCall
            {
                sufferingGuest = guest,
                roomNumber = sufferingRoom.roomNumber,
                timer = randomDelay
            });

            Debug.Log($"[Call Scheduled] {guest.guestName} in room {sufferingRoom.roomNumber} queued to call in {randomDelay:F1}s.");
        }

        private void Update()
        {
            if (_pendingCalls == null || _pendingCalls.Count == 0) return;
            if (DayManager.Instance != null && !DayManager.Instance.IsTimeFlowing) return;

            // Someone is already ringing or being talked to — every other pending call stays
            // frozen (its timer does not tick) until that one is fully resolved.
            //
            // A guest who was promised a room move (GuestAwaitingMove) also freezes every other
            // pending call, even though their own call has already ended and _activeCall is back
            // to null. Until staff actually performs the move, "additional 전화 송신" must not
            // happen — same freeze rule as an active call, just keyed off a different field.
            if (_activeCall != null || GuestAwaitingMove != null) return;

            // Once there's nothing left for the player to actively do this phase — either the clock
            // already ran out, or every guest who was going to arrive already has — there's no point
            // making them sit through each call's original random cooldown. Drain the queue one call
            // at a time, as fast as they can be answered, instead of waiting it out.
            bool nothingElseToWaitFor = ComputeNothingElseToWaitFor();

            for (int i = _pendingCalls.Count - 1; i >= 0; i--)
            {
                var call = _pendingCalls[i];

                // Verify suffering guest is still occupied in the room
                var room = GetRoom(call.roomNumber);
                if (room == null || room.occupant != call.sufferingGuest || room.status != RoomStatus.Occupied)
                {
                    _pendingCalls.RemoveAt(i);
                    continue;
                }

                // The guest's own problem may have resolved itself before they got a chance to call —
                // e.g. the noisy neighbor checked out or was moved to a different room, clearing every
                // nuisance source affecting this room. If nothing is bothering them anymore, treat it
                // as if they never had to call at all: no ring, no hasCalledNuisance, no rating hit.
                if (room.incomingNuisanceSources.Count == 0)
                {
                    Debug.Log($"[RoomManager] {call.sufferingGuest.guestName}'s nuisance in room {room.roomNumber} resolved itself before they could call — cancelling.");
                    _pendingCalls.RemoveAt(i);
                    continue;
                }

                if (nothingElseToWaitFor)
                    call.timer = 0f;
                else
                    call.timer -= Time.deltaTime;

                int secondsLeft = Mathf.CeilToInt(call.timer);
                if (secondsLeft > 0 && secondsLeft != call.lastLoggedSecond)
                {
                    call.lastLoggedSecond = secondsLeft;
                    Debug.Log($"[{call.sufferingGuest.guestName}] {secondsLeft}s left until call.");
                }

                if (call.timer <= 0f)
                {
                    if (call.sufferingGuest != null && !call.sufferingGuest.hasCalledNuisance)
                    {
                        call.sufferingGuest.hasCalledNuisance = true;
                        call.sufferingGuest.nuisanceComplaintCount++;
                        call.isRinging = true;
                        _activeCall = call;
                        Debug.Log($"{call.sufferingGuest.guestName} called.");
                        NotifyPhoneCallRinging(call.sufferingGuest, call.roomNumber);
                        break; // only one call becomes active per frame; the rest stay frozen
                    }

                    _pendingCalls.RemoveAt(i);
                }
            }
        }

        private bool ComputeNothingElseToWaitFor()
        {
            return DayManager.Instance != null &&
                (DayManager.Instance.PhaseTimeRemaining <= 0f || DayManager.Instance.NoMoreArrivalsThisPhase);
        }

        /// <summary>
        /// True if another call is already queued and will start ringing on the very next Update tick
        /// once _activeCall clears (i.e. the queue is in "drain instantly, one at a time" mode — see
        /// ComputeNothingElseToWaitFor/Update). BGMManager uses this to avoid un-ducking BGM only to
        /// immediately duck it again a moment later for the next call — at night, calls are frequently
        /// queued back-to-back this way (arrival queue drains early, so nothingElseToWaitFor kicks in),
        /// and un-duck/re-duck within a fraction of a second reads as an audible BGM stutter/cut rather
        /// than two separate, deliberate dips.
        /// </summary>
        public bool HasImmediatelyQueuedCall()
        {
            return _pendingCalls != null && _pendingCalls.Count > 0 && ComputeNothingElseToWaitFor();
        }

        private void NotifyPhoneCallRinging(Animal guest, int roomNumber)
        {
            OnCallRinging?.Invoke(guest, roomNumber);
            var phoneCtrl = FindFirstObjectByType<PhoneCallController>(FindObjectsInactive.Include);
            if (phoneCtrl != null)
            {
                phoneCtrl.OnCallRinging(guest, roomNumber);
            }
        }

        private void NotifyPhoneCallEnded(Animal guest, int roomNumber, bool wasAnswered)
        {
            OnCallEnded?.Invoke(guest, roomNumber, wasAnswered);
            var phoneCtrl = FindFirstObjectByType<PhoneCallController>(FindObjectsInactive.Include);
            if (phoneCtrl != null)
            {
                phoneCtrl.OnCallEnded(guest, roomNumber, wasAnswered);
            }
        }

        /// <summary>
        /// Called the instant the player answers — stops the ringing shake/sfx/timeout on the Phone
        /// prop, but deliberately does NOT sink/deactivate it (that's NotifyPhoneCallEnded's job, now
        /// only called once the phone dialogue itself has actually finished — see
        /// ResolvePhoneCallDialogue). Answering used to immediately fire NotifyPhoneCallEnded, which
        /// made the Phone/PhoneLine props disappear before the conversation even started.
        /// </summary>
        private void NotifyPhoneCallAnswered(Animal guest, int roomNumber)
        {
            var phoneCtrl = FindFirstObjectByType<PhoneCallController>(FindObjectsInactive.Include);
            if (phoneCtrl != null)
            {
                phoneCtrl.OnCallAnswered();
            }
        }

        /// <summary>
        /// Answers the ringing call for the specified guest. The call leaves the queue immediately,
        /// but stays "active" (still freezing every other pending call) until the phone conversation
        /// itself finishes — see ResolvePhoneCallDialogue, called once DialogueManager reports the
        /// phone-call dialogue has ended.
        /// </summary>
        public bool AnswerCall(Animal guest)
        {
            if (guest == null) return false;
            var call = _pendingCalls.Find(c => c.sufferingGuest == guest && c.isRinging);
            if (call != null)
            {
                Debug.Log($"[Call Answered] Player answered {guest.guestName}'s call.");
                int roomNum = call.roomNumber;
                _pendingCalls.Remove(call);
                NotifyPhoneCallAnswered(guest, roomNum);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Answers the ringing call for the specified room number.
        /// </summary>
        public bool AnswerCall(int roomNumber)
        {
            var call = _pendingCalls.Find(c => c.roomNumber == roomNumber && c.isRinging);
            if (call != null)
            {
                Debug.Log($"[Call Answered] Player answered {call.sufferingGuest.guestName}'s call in room {roomNumber}.");
                var guest = call.sufferingGuest;
                _pendingCalls.Remove(call);
                NotifyPhoneCallAnswered(guest, roomNumber);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Cancels/misses the active ringing call for the specified guest when unanswered.
        /// A missed call counts as an unresolved complaint for rating purposes.
        /// </summary>
        public bool CancelCall(Animal guest)
        {
            if (guest == null) return false;
            var call = _pendingCalls.Find(c => c.sufferingGuest == guest && c.isRinging);
            if (call != null)
            {
                Debug.Log($"[Call Missed] {guest.guestName}'s call was cancelled (unanswered).");
                int roomNum = call.roomNumber;
                _pendingCalls.Remove(call);
                guest.nuisanceResolution = Animal.NuisanceResolution.Unresolved;
                if (_activeCall == call) _activeCall = null;
                NotifyPhoneCallEnded(guest, roomNum, false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Cancels/misses the active ringing call for the specified room number when unanswered.
        /// </summary>
        public bool CancelCall(int roomNumber)
        {
            var call = _pendingCalls.Find(c => c.roomNumber == roomNumber && c.isRinging);
            if (call != null)
            {
                Debug.Log($"[Call Missed] {call.sufferingGuest.guestName}'s call in room {roomNumber} was cancelled (unanswered).");
                var guest = call.sufferingGuest;
                _pendingCalls.Remove(call);
                if (guest != null) guest.nuisanceResolution = Animal.NuisanceResolution.Unresolved;
                if (_activeCall == call) _activeCall = null;
                NotifyPhoneCallEnded(guest, roomNumber, false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called once the phone-call dialogue for the currently active call has fully finished
        /// (forwarded from DialogueManager.OnPhoneCallDialogueEnd via DayManager). Releases the freeze
        /// on the remaining pending calls. If staff promised a room move, the complaint is only marked
        /// Resolved once that move actually happens (see ResolveRoomMove) — until then it counts as
        /// Unresolved, same as if no room had been offered at all.
        /// </summary>
        public void ResolvePhoneCallDialogue(Animal guest, string exitNodeId)
        {
            if (guest == null) return;

            // If the player already relocated this guest WHILE they were still on the phone (see
            // RoomUI.AssignRoomForActivePhoneCall / DialogueManager.NotifyRoomAssigned), the "빈 방으로
            // 옮겨 드릴게요" choice only became clickable because the move already happened — don't
            // stomp that Resolved status back to Unresolved, and don't queue a second, redundant move.
            bool alreadyResolved = guest.nuisanceResolution == Animal.NuisanceResolution.Resolved;
            if (!alreadyResolved)
            {
                guest.nuisanceResolution = Animal.NuisanceResolution.Unresolved;

                if (exitNodeId == "phone_exit_move")
                {
                    GuestAwaitingMove = guest;
                    Debug.Log($"[RoomManager] {guest.guestName} was promised a room move.");
                }
            }
            else
            {
                Debug.Log($"[RoomManager] {guest.guestName} was already moved to a new room before hanging up.");
            }

            int roomNumber = (_activeCall != null && _activeCall.sufferingGuest == guest)
                ? _activeCall.roomNumber
                : (GetRoomByOccupant(guest)?.roomNumber ?? 0);

            if (_activeCall != null && _activeCall.sufferingGuest == guest)
            {
                _activeCall = null;
            }

            // The conversation is genuinely over now — hang up the Phone/PhoneLine props. This used to
            // happen the instant the call was ANSWERED instead, so the phone looked like it had already
            // hung up while the player was still mid-conversation.
            NotifyPhoneCallEnded(guest, roomNumber, true);
        }

        /// <summary>
        /// Called by RoomUI once it has actually moved a guest who was promised a new room during a
        /// nuisance phone call. Upgrades the complaint from Unresolved to Resolved for rating purposes.
        /// </summary>
        public void ResolveRoomMove(Animal guest)
        {
            if (guest == null) return;

            guest.nuisanceResolution = Animal.NuisanceResolution.Resolved;
            if (GuestAwaitingMove == guest) GuestAwaitingMove = null;

            // hasCalledNuisance was already reset in MoveAnimal(), before nuisance for the new room was
            // evaluated — so a fresh call could be scheduled immediately if the new room already had
            // trouble waiting. See MoveAnimal() for why the reset has to happen there and not here.
            Debug.Log($"[RoomManager] {guest.guestName}'s room move resolved the complaint. " +
                      $"Total complaints this stay: {guest.nuisanceComplaintCount}.");
        }

        /// <summary>
        /// Returns true if any incoming call is currently ringing.
        /// </summary>
        public bool HasRingingCall()
        {
            return _pendingCalls != null && _pendingCalls.Exists(c => c.isRinging);
        }

        /// <summary>
        /// Returns true if there is any call still queued, ringing, or being handled, OR a guest is
        /// still waiting on a room move that was promised during a call — i.e. the current time slot
        /// must not advance yet.
        /// </summary>
        public bool HasPendingCalls()
        {
            return (_pendingCalls != null && _pendingCalls.Count > 0) || _activeCall != null || GuestAwaitingMove != null;
        }

        /// <summary>
        /// Returns the currently active ringing call, if any.
        /// </summary>
        public PendingCall GetActiveRingingCall()
        {
            return _pendingCalls?.Find(c => c.isRinging);
        }
    }
}
