using UnityEngine;

namespace AnimalHotel
{
    public static class InputHelper
    {
        public static void GetInput(out Vector2 pos, out bool downThisFrame)
        {
            pos = Vector2.zero;
            downThisFrame = false;

#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null)
            {
                pos = pointer.position.ReadValue();
                downThisFrame = pointer.press.wasPressedThisFrame;
                return;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            pos = Input.mousePosition;
            downThisFrame = Input.GetMouseButtonDown(0);
#endif
        }
    }
}
