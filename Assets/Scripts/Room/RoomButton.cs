using UnityEngine;

namespace AnimalHotel.Counter
{
    [RequireComponent(typeof(Collider2D))]
    public class RoomButton : MonoBehaviour
    {
        [SerializeField] public int roomNumber;
        [SerializeField] private RoomUI roomUI;

        private Camera _cachedCam;

        private void Update()
        {
            if (_cachedCam == null) _cachedCam = Camera.main;
            if (_cachedCam == null) return;

            Vector2 mousePos; bool mouseDown;
            GetMouseInput(out mousePos, out mouseDown);
            if (!mouseDown) return;

            Vector3 w3 = _cachedCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
            if (GetComponent<Collider2D>().OverlapPoint(new Vector2(w3.x, w3.y)))
                roomUI.OnRoomClicked(roomNumber);
        }

        private static void GetMouseInput(out Vector2 pos, out bool downThisFrame)
        {
            AnimalHotel.InputHelper.GetInput(out pos, out downThisFrame);
        }
    }
}