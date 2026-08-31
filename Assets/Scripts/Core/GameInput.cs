using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PolyStrike.Core
{
    public static class GameInput
    {
        private static bool Blocked => DeveloperConsole.IsOpen;

        public static Vector2 Movement
        {
            get
            {
                if (Blocked) return Vector2.zero;
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
                if (Blocked) return Vector2.zero;
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
                if (Blocked) return false;
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
                if (Blocked) return false;
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
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.leftShiftKey.isPressed == true;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }

        public static bool VoiceHeld
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.kKey.isPressed == true;
#else
                return Input.GetKey(KeyCode.K);
#endif
            }
        }

        public static bool FireHeld
        {
            get
            {
                if (Blocked) return false;
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
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.leftButton.wasPressedThisFrame == true;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        public static bool FireReleased
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.leftButton.wasReleasedThisFrame == true;
#else
                return Input.GetMouseButtonUp(0);
#endif
            }
        }

        public static bool SecondaryFireHeld
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.rightButton.isPressed == true;
#else
                return Input.GetMouseButton(1);
#endif
            }
        }

        public static bool SecondaryFirePressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.rightButton.wasPressedThisFrame == true;
#else
                return Input.GetMouseButtonDown(1);
#endif
            }
        }

        public static bool SecondaryFireReleased
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Mouse.current?.rightButton.wasReleasedThisFrame == true;
#else
                return Input.GetMouseButtonUp(1);
#endif
            }
        }

        public static bool UseHeld
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.eKey.isPressed == true;
#else
                return Input.GetKey(KeyCode.E);
#endif
            }
        }

        public static bool BuyPressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.bKey.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.B);
#endif
            }
        }

        public static bool DropPressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.gKey.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.G);
#endif
            }
        }

        public static bool ReloadPressed
        {
            get
            {
                if (Blocked) return false;
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
                if (Blocked) return false;
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
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit2Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha2);
#endif
            }
        }

        public static bool UtilityPressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit4Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha4);
#endif
            }
        }

        public static bool BombPressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit5Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha5);
#endif
            }
        }

        public static bool HeGrenadePressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit6Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha6);
#endif
            }
        }

        public static bool FlashbangPressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit7Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha7);
#endif
            }
        }

        public static bool SmokePressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit8Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha8);
#endif
            }
        }

        public static bool MolotovPressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.digit0Key.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Alpha0);
#endif
            }
        }

        public static bool ScoreboardHeld
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.tabKey.isPressed == true;
#else
                return Input.GetKey(KeyCode.Tab);
#endif
            }
        }

        public static bool EscapePressed
        {
            get
            {
                if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current?.escapeKey.wasPressedThisFrame == true;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }
    }
}
