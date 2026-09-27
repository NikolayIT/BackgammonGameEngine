namespace Backgammon.Trainer
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Asks Windows not to throttle this process (EcoQoS). Without it, a long background run can end up on the slow cores
    /// and take several times longer (seen with the Belot trainer). It does nothing on other systems.
    /// </summary>
    internal static partial class NativeMethods
    {
        private const int ProcessPowerThrottling = 4;
        private const uint CurrentVersion = 1;
        private const uint ExecutionSpeed = 0x1;

        public static void DisablePowerThrottling()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var state = new State { Version = CurrentVersion, ControlMask = ExecutionSpeed, StateMask = 0 };
            SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<State>());
        }

        [LibraryImport("kernel32.dll")]
        private static partial IntPtr GetCurrentProcess();

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetProcessInformation(IntPtr process, int informationClass, ref State information, uint size);

        [StructLayout(LayoutKind.Sequential)]
        private struct State
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }
    }
}
