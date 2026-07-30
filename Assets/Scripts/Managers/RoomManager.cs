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

            EvaluateNuisanceOnAssignment(room);

            return true;
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

            if (guest.hasCalledNuisance) return;

            foreach (var call in _pendingCalls)
            {
                if (call.sufferingGuest == guest) return;
            }

            float maxCallDelay = 10f;
            if (DayManager.Instance != null)
            {
                // 1. Must call before the suffering guest checks out
                float sufferingRemaining = DayManager.Instance.GetRemainingStaySeconds(guest);
                maxCallDelay = sufferingRemaining;

                // 2. Must call before any nuisance-causing guest checks out
                if (sufferingRoom.incomingNuisanceSources.Count > 0)
                {
                    foreach (int sourceRoomNum in sufferingRoom.incomingNuisanceSources)
                    {
                        var sourceRoom = GetRoom(sourceRoomNum);
                        if (sourceRoom != null && sourceRoom.occupant != null)
                        {
                            float causingRemaining = DayManager.Instance.GetRemainingStaySeconds(sourceRoom.occupant);
                            if (causingRemaining > 0f && causingRemaining < maxCallDelay)
                            {
                                maxCallDelay = causingRemaining;
                            }
                        }
                    }
                }
            }

            // Reserve safety margin (0.5s) so call happens before either guest leaves
            float safeMaxDelay = Mathf.Max(0.5f, maxCallDelay - 0.5f);

            float minGap = 2.0f;
            float randomDelay = Random.Range(0f, safeMaxDelay);

            // Pick a random delay that is at least minGap seconds apart from all existing pending calls
            int maxAttempts = 50;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                bool collision = false;
                foreach (var existingCall in _pendingCalls)
                {
                    if (Mathf.Abs(randomDelay - existingCall.timer) < minGap)
                    {
                        collision = true;
                        break;
                    }
                }

                if (!collision) break;
                randomDelay = Random.Range(0f, safeMaxDelay);
            }

            _pendingCalls.Add(new PendingCall
            {
                sufferingGuest = guest,
                roomNumber = sufferingRoom.roomNumber,
                timer = randomDelay
            });

            Debug.Log($"[Call Scheduled] {guest.guestName} in room {sufferingRoom.roomNumber} scheduled to call in {randomDelay:F1}s (must call before checkout in {maxCallDelay:F1}s).");
        }

        private void Update()
        {
            if (_pendingCalls == null || _pendingCalls.Count == 0) return;
            if (DayManager.Instance != null && !DayManager.Instance.IsTimeFlowing) return;

            bool callTriggeredThisFrame = false;
            float minGap = 2.0f;

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

                if (!call.isRinging)
                {
                    call.timer -= Time.deltaTime;

                    int secondsLeft = Mathf.CeilToInt(call.timer);
                    if (secondsLeft > 0 && secondsLeft != call.lastLoggedSecond)
                    {
                        call.lastLoggedSecond = secondsLeft;
                        Debug.Log($"[{call.sufferingGuest.guestName}] {secondsLeft}s left until call.");
                    }

                    if (call.timer <= 0f)
                    {
                        // Stagger calls so multiple calls do not ring at the exact same time
                        if (callTriggeredThisFrame)
                        {
                            call.timer = minGap;
                            call.lastLoggedSecond = -1;
                            continue;
                        }

                        if (call.sufferingGuest != null && !call.sufferingGuest.hasCalledNuisance)
                        {
                            call.sufferingGuest.hasCalledNuisance = true;
                            call.isRinging = true;
                            Debug.Log($"{call.sufferingGuest.guestName} called.");
                            callTriggeredThisFrame = true;
                            NotifyPhoneCallRinging(call.sufferingGuest, call.roomNumber);
                        }
                        else
                        {
                            _pendingCalls.RemoveAt(i);
                        }
                    }
                }
            }
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
        /// Answers the ringing call for the specified guest.
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
                NotifyPhoneCallEnded(guest, roomNum, true);
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
                NotifyPhoneCallEnded(guest, roomNumber, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Cancels/misses the active ringing call for the specified guest when unanswered.
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
                NotifyPhoneCallEnded(guest, roomNumber, false);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if any incoming call is currently ringing.
        /// </summary>
        public bool HasRingingCall()
        {
            return _pendingCalls != null && _pendingCalls.Exists(c => c.isRinging);
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
