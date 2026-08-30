using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PolyStrike.Core
{
    public static class GameInput
    {
        public static Vector2 Movement
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                if (keyboard == null)
                    return Vector2.zero;

                var x = 0f;
                var y = 0f;
                if (keyboard.aKey.isPressed) x -= 1f;
                if (keyboard.dKey.isPressed) x += 1f;
                if (keyboard.sKey.isPressed) y -= 1f;
                if (keyboard.wKey.isPressed) y += 1f;
                return new Vector2(x, y);
#else
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            }
        }

        public static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.delta.ReadValue() ?? Vector2.zero;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
            }
        }

        public static bool JumpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.spaceKey.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }

        public static bool CrouchHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed);
#else
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
#endif
            }
        }

        public static bool WalkHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.leftShiftKey.isPressed == true;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }

        public static bool FireHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.leftButton.isPressed == true;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        public static bool FirePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.leftButton.wasPressedThisFrame == true;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool ReloadPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.rKey.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.R);
#endif
            }
        }

        public static bool Weapon1Pressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit1Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha1);
#endif
            }
        }

        public static bool Weapon2Pressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit2Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha2);
#endif
            }
        }

        public static bool EscapePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }
    }
}
