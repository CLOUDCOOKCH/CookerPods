using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using CookerPods.Diagnostic.Core;

namespace CookerPods.Diagnostic.Windows;

internal sealed record ControlDevice(string Label, string Path);

internal sealed class DriverAapTransport(ControlDevice device) : IAapTransport
{
    private const uint Status = 0x22e000, Connect = 0x22e004, Send = 0x22e00c, Receive = 0x22e010;
    private SafeFileHandle? handle;
    private int disposed;
    internal static readonly Guid InterfaceId = new("504f4443-4f4f-4b43-8940-55204aa6f610");

    public async ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var opened = await Task.Run(() =>
        {
            var value = CreateFile(device.Path, 0xc0000000, 0, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (value.IsInvalid) { int error = Marshal.GetLastPInvokeError(); value.Dispose(); throw new Win32Exception(error); }
            return value;
        }, cancellationToken);
        if (Interlocked.CompareExchange(ref handle, opened, null) is not null) { opened.Dispose(); throw new InvalidOperationException("Already opened."); }
        if (Volatile.Read(ref disposed) != 0) { await DisposeAsync(); throw new ObjectDisposedException(nameof(DriverAapTransport)); }
        byte[] status = await IoAsync(Status, null, 16, cancellationToken);
        if (status.Length != 16 || BinaryPrimitives.ReadUInt32LittleEndian(status) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(status.AsSpan(8)) != AapProtocol.MaxPacketBytes)
            throw new InvalidOperationException("Incompatible control driver ABI.");
        await IoAsync(Connect, null, 0, cancellationToken);
    }
    public async ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken)
    {
        if (packet.Length is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(packet));
        await IoAsync(Send, packet.ToArray(), 0, cancellationToken);
    }
    public async ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken)
    {
        try { return await IoAsync(Receive, null, AapProtocol.MaxPacketBytes, cancellationToken); }
        catch (Win32Exception error) when (error.NativeErrorCode is 121 or 258 or 1460) { return ReadOnlyMemory<byte>.Empty; }
    }
    private async Task<byte[]> IoAsync(uint code, byte[]? input, int outputSize, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var current = handle ?? throw new InvalidOperationException("Driver is not open.");
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            byte[] output = new byte[outputSize];
            using var registration = token.Register(() => TryCancel(current));
            if (!DeviceIoControl(current, code, input, input?.Length ?? 0, output, output.Length, out uint count, IntPtr.Zero))
            {
                int error = Marshal.GetLastPInvokeError();
                token.ThrowIfCancellationRequested();
                throw new Win32Exception(error);
            }
            token.ThrowIfCancellationRequested();
            if (count > output.Length) throw new InvalidOperationException("Driver returned an invalid byte count.");
            return output.AsSpan(0, (int)count).ToArray();
        }, token);
    }
    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref disposed, 1);
        var old = Interlocked.Exchange(ref handle, null);
        if (old is not null)
        {
            TryCancel(old);
            // Closing the only handle invokes driver cleanup and channel closure.
            await Task.Run(old.Dispose);
        }
    }

    private static void TryCancel(SafeFileHandle value)
    {
        try { if (!value.IsClosed && !value.IsInvalid) _ = CancelIoEx(value, IntPtr.Zero); }
        catch (ObjectDisposedException) { /* Another shutdown path already closed it. */ }
    }

    internal static IReadOnlyList<ControlDevice> Enumerate()
    {
        var guid = InterfaceId;
        using var devices = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (devices.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var result = new List<ControlDevice>();
        for (uint index = 0; index < 8; index++)
        {
            var entry = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
            if (!SetupDiEnumDeviceInterfaces(devices, IntPtr.Zero, ref guid, index, ref entry))
            {
                int error = Marshal.GetLastPInvokeError();
                if (error == 259) break;
                throw new Win32Exception(error);
            }
            _ = SetupDiGetDeviceInterfaceDetail(devices, ref entry, IntPtr.Zero, 0, out uint size, IntPtr.Zero);
            if (size is < 8 or > 65536) throw new InvalidOperationException("Invalid driver interface size.");
            var memory = Marshal.AllocHGlobal((int)size);
            try
            {
                Marshal.WriteInt32(memory, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(devices, ref entry, memory, size, out _, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                string? path = Marshal.PtrToStringUni(memory + 4);
                if (string.IsNullOrEmpty(path) || path.Length > 32760) throw new InvalidOperationException("Invalid device path.");
                result.Add(new($"Paired AirPods control service {index + 1}", path));
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        return result;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct InterfaceData { public int Size; public Guid ClassGuid; public uint Flags; public IntPtr Reserved; }
    private sealed class DeviceInfoHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public DeviceInfoHandle() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern DeviceInfoHandle SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(DeviceInfoHandle set, IntPtr device, ref Guid guid, uint index, ref InterfaceData entry);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(DeviceInfoHandle set, ref InterfaceData entry, IntPtr detail, uint size, out uint required, IntPtr device);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[]? input, int inputSize, byte[] output, int outputSize, out uint returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
}
