using UnityEngine;

namespace AnimalHotel.Counter
{
    public enum RoomStatus
    {
        Vacant,
        Occupied,
        NeedsExamination,
        NeedsCleaning
    }

    public class RoomData
    {
        public int roomNumber;
        public Animal occupant;
        public RoomStatus status;
        public Animal lastOccupant;
        public bool requiresAdvancedCleaning;

    }

    public class RoomManager : MonoBehaviour
    {
        public static readonly int RoomCount = 10;

        private RoomData[] _rooms;

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
            return true;
        }

        public void VacateRoom(int roomNumber)
        {
            var room = GetRoom(roomNumber);
            var departingGuest = room.occupant;
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
                MarkRoomVacant(room);
                Debug.Log($"[RoomManager] Room {roomNumber} advanced-cleaned - ready for assignment.");
                return true;
            }

            Debug.LogWarning($"[RoomManager] Room {roomNumber} does not need cleaning.");
            return false;
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
            return guest.LeavesOdour || guest.CausesDamage;
        }



    }
}