using System.Reflection;
using System.Runtime.InteropServices;

namespace Lucid.Rtc.Native;

/// <summary>
/// Native FFI methods for Lucid.Rtc library.
/// </summary>
internal static class NativeMethods
{
    private const string DllName = "lucid_rtc";

    static NativeMethods()
    {
        // Register custom resolver for native library loading
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != DllName)
            return IntPtr.Zero;

        // Try to load from runtimes folder
        var rid = RuntimeIdentifier;
        var fileName = GetNativeFileName();

        // Check various locations
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "runtimes", rid, "native", fileName),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (NativeLibrary.TryLoad(path, out var handle))
                    return handle;
            }
        }

        // Fallback to default resolution
        if (NativeLibrary.TryLoad(DllName, assembly, searchPath, out var defaultHandle))
            return defaultHandle;

        return IntPtr.Zero;
    }

    private static string RuntimeIdentifier =>
        RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "win-x64",
            Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "win-arm64",
            Architecture.X86 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "win-x86",
            Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-x64",
            Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-arm64",
            Architecture.Arm when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-arm",
            Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx-x64",
            Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx-arm64",
            _ => "unknown"
        };

    private static string GetNativeFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "lucid_rtc.dll" :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "liblucid_rtc.so" :
        "liblucid_rtc.dylib";

    // Client lifecycle
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lucid_rtc_create_client(IntPtr configJson);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lucid_rtc_destroy_client(IntPtr handle);

    // SDP negotiation
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lucid_rtc_create_offer(IntPtr handle, IntPtr peerId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lucid_rtc_set_remote_offer(IntPtr handle, IntPtr peerId, IntPtr sdp);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_set_remote_answer(IntPtr handle, IntPtr peerId, IntPtr sdp);

    // ICE candidates
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_add_ice_candidate(IntPtr handle, IntPtr peerId, IntPtr candidate, IntPtr sdpMid, int sdpMlineIndex);

    // Messaging
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_send_message(IntPtr handle, IntPtr peerId, byte[] data, nuint len);

    // Broadcast
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_broadcast(IntPtr handle, byte[] data, nuint len);

    // Events
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lucid_rtc_poll_events(IntPtr handle);

    // Connection state
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_is_connected(IntPtr handle, IntPtr peerId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_wait_for_ice_connected(IntPtr handle, IntPtr peerId);

    // Peer management
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_close_peer(IntPtr handle, IntPtr peerId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lucid_rtc_close_all(IntPtr handle);

    // Memory management
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lucid_rtc_free_string(IntPtr s);

    // Version
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lucid_rtc_version();
}
