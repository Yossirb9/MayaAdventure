using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MayaAdventure
{
    /// <summary>Keyboard / mouse / gamepad input that works with either Unity input backend.</summary>
    public static class Controls
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 Move =>
            Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + TouchControls.Move, 1f);
        public static Vector2 Look => TouchControls.Active ? TouchControls.Look
                                                           : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f;
        public static float Zoom => Input.mouseScrollDelta.y + TouchControls.Pinch;
        public static bool JumpDown => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0) || TouchControls.ADown;
        public static bool JumpHeld => Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0) || TouchControls.AHeld;
        public static bool PoundDown => Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.E) ||
                                        Input.GetKeyDown(KeyCode.JoystickButton1) || TouchControls.BDown;
        public static bool PoundHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.JoystickButton1) ||
                                        TouchControls.BHeld;
        public static bool PunchDown => Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton2) || 
                                        (!TouchControls.Active && Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0));
        public static bool WalkHeld => Input.GetKey(KeyCode.LeftShift);
        // Escape is also the Android back button.
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || TouchControls.PauseTap;
        public static bool ConfirmDown => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                                          Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton7) ||
                                          Input.GetKeyDown(KeyCode.JoystickButton0) || TouchControls.AnyTap;
        public static bool ClickDown => !TouchControls.Active && Input.GetMouseButtonDown(0);
        public static bool RespawnDown => Input.GetKeyDown(KeyCode.R) || TouchControls.RespawnTap;
        public static bool NewGameDown => Input.GetKeyDown(KeyCode.N) || TouchControls.NewGameTap;
#elif ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        public static Vector2 Move
        {
            get
            {
                var v = Vector2.zero;
                if (K != null)
                {
                    if (K.aKey.isPressed || K.leftArrowKey.isPressed) v.x -= 1f;
                    if (K.dKey.isPressed || K.rightArrowKey.isPressed) v.x += 1f;
                    if (K.sKey.isPressed || K.downArrowKey.isPressed) v.y -= 1f;
                    if (K.wKey.isPressed || K.upArrowKey.isPressed) v.y += 1f;
                }
                if (G != null) v += G.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static Vector2 Look
        {
            get
            {
                var v = Vector2.zero;
                if (M != null) v += M.delta.ReadValue() * 0.3f;
                if (G != null) v += G.rightStick.ReadValue() * (160f * Time.unscaledDeltaTime);
                return v;
            }
        }

        public static float Zoom => M != null ? M.scroll.ReadValue().y / 120f : 0f;
        public static bool JumpDown => (K != null && K.spaceKey.wasPressedThisFrame) || (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool JumpHeld => (K != null && K.spaceKey.isPressed) || (G != null && G.buttonSouth.isPressed);
        public static bool PoundDown => (K != null && (K.leftCtrlKey.wasPressedThisFrame || K.eKey.wasPressedThisFrame)) ||
                                        (G != null && (G.buttonEast.wasPressedThisFrame || G.leftTrigger.wasPressedThisFrame));
        public static bool PoundHeld => (K != null && (K.leftCtrlKey.isPressed || K.eKey.isPressed)) || (G != null && (G.buttonEast.isPressed || G.leftTrigger.isPressed));
        public static bool PunchDown => (K != null && K.fKey.wasPressedThisFrame) || (G != null && G.buttonWest.wasPressedThisFrame) ||
                                        (M != null && Cursor.lockState == CursorLockMode.Locked && M.leftButton.wasPressedThisFrame);
        public static bool WalkHeld => K != null && K.leftShiftKey.isPressed;
        public static bool PauseDown => (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool ConfirmDown => (K != null && (K.enterKey.wasPressedThisFrame || K.numpadEnterKey.wasPressedThisFrame)) ||
                                          (M != null && M.leftButton.wasPressedThisFrame) ||
                                          (G != null && (G.startButton.wasPressedThisFrame || G.buttonSouth.wasPressedThisFrame));
        public static bool ClickDown => M != null && M.leftButton.wasPressedThisFrame;
        public static bool RespawnDown => K != null && K.rKey.wasPressedThisFrame;
        public static bool NewGameDown => K != null && K.nKey.wasPressedThisFrame;
#else
        public static Vector2 Move => Vector2.zero;
        public static Vector2 Look => Vector2.zero;
        public static float Zoom => 0f;
        public static bool JumpDown => false;
        public static bool JumpHeld => false;
        public static bool PoundDown => false;
        public static bool PoundHeld => false;
        public static bool PunchDown => false;
        public static bool WalkHeld => false;
        public static bool PauseDown => false;
        public static bool ConfirmDown => false;
        public static bool ClickDown => false;
        public static bool RespawnDown => false;
        public static bool NewGameDown => false;
#endif
    }
}
