using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Input from a script instead of a player (used by the trailer recorder). While <see cref="Active"/>,
    /// Maya moves along <see cref="WorldMove"/> and the button flags are merged into <see cref="Controls"/>;
    /// she also can't be hurt, so a recording never breaks off.
    /// </summary>
    public static class ScriptedInput
    {
        public static bool Active;
        public static Vector3 WorldMove;
        public static bool JumpDown, JumpHeld, PoundDown, PoundHeld, PunchDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            WorldMove = Vector3.zero;
            JumpDown = JumpHeld = PoundDown = PoundHeld = PunchDown = false;
        }
    }
}
