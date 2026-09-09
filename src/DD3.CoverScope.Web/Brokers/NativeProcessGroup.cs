using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

#pragma warning disable CS0649 // Fields in native layout structures are populated by marshalling.

namespace DD3.CoverScope.Brokers;

// A group is established before the supervisor is allowed to launch repository tools.
internal sealed class NativeProcessGroup : IDisposable
{
    private readonly int processId;
    private IntPtr job;

    public NativeProcessGroup(Process process)
    {
        processId = process.Id;
        if (!OperatingSystem.IsWindows()) { return; }
        job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) { throw new Win32Exception(); }
        var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } };
        if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()) || !AssignProcessToJobObject(job, process.Handle))
        { var error = new Win32Exception(); Dispose(); throw error; }
    }

    public static void PrepareSupervisor()
    {
        if (!OperatingSystem.IsWindows() && setsid() < 0) { throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot create an owned process group."); }
    }

    public void Terminate()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!TerminateJobObject(job, 1)) { throw new Win32Exception(); }
        }
        else if (kill(-processId, 9) != 0 && Marshal.GetLastPInvokeError() != 3)
        { throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot terminate the owned process group."); }
    }

    public bool IsEmpty()
    {
        if (!OperatingSystem.IsWindows())
        {
            if (kill(-processId, 0) == 0) { return false; }
            if (Marshal.GetLastPInvokeError() == 3) { return true; }
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot verify the owned process group.");
        }
        if (!QueryInformationJobObject(job, 1, out Accounting info, (uint)Marshal.SizeOf<Accounting>(), IntPtr.Zero)) { throw new Win32Exception(); }
        return info.ActiveProcesses == 0;
    }

    public void Dispose()
    {
        if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; }
    }

    internal static void StopSupervisorGroup()
    {
        if (!OperatingSystem.IsWindows()) { kill(-Environment.ProcessId, 9); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("libc", SetLastError = true)] private static extern int setsid();
    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int kind, ref ExtendedLimits limits, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint code);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(IntPtr job, int kind, out Accounting info, uint length, IntPtr returnedLength);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
