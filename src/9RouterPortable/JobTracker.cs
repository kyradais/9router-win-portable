using System;
using System.Runtime.InteropServices;

namespace NineRouterPortable
{
    public static class JobTracker
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public IntPtr MinimumWorkingSetSize;
            public IntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public IntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadWriteOperationCount;
            public ulong OtherTransferCount;
            public ulong WriteTransferCount;
            public ulong TransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        private static IntPtr _jobHandle = IntPtr.Zero;

        public static void Initialize()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle != IntPtr.Zero)
                {
                    var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                    int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                    IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                    try
                    {
                        Marshal.StructureToPtr(info, extendedInfoPtr, false);
                        SetInformationJobObject(_jobHandle, JobObjectExtendedLimitInformation, extendedInfoPtr, (uint)length);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(extendedInfoPtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to initialize Job Object: " + ex.Message);
            }
        }

        public static void AddProcess(IntPtr processHandle)
        {
            if (_jobHandle == IntPtr.Zero || processHandle == IntPtr.Zero) return;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            try
            {
                AssignProcessToJobObject(_jobHandle, processHandle);
            }
            catch (Exception ex)
            {
                Logger.Log("Error assigning process to Job Object: " + ex.Message);
            }
        }
    }
}
