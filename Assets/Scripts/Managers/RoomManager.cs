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
            var room = GetRoom(roomNumber);
            if (room.status != RoomStatus.Vacant)
            {
                Debug.LogWarning($"[RoomManager] Room {roomNumber} is not vacant.");
                return false;
            }
            room.occupant = guest;
            room.status = RoomStatus.Occupied;
            Debug.Log($"[RoomManager] Room {roomNumber} assigned to {guest.guestName}.");
            return true;
        }

        public void VacateRoom(int roomNumber)
        {
            var room = GetRoom(roomNumber);
            room.occupant = null;
            room.status = RoomStatus.NeedsExamination;
            Debug.Log($"[RoomManager] Room {roomNumber} vacated — needs examination.");
        }
    }
}