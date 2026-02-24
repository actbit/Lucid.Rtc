using System.Reflection;
using System.Runtime.InteropServices;

namespace Lucid.Rtc.Native;

/// <summary>
/// Native FFI methods for WebRTC Sharp library.
/// </summary>
internal static class NativeMethods
{
    private const string DllName = "webrtc_sharp";

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
            Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-x64",
            Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-arm64",
            Architecture.X64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx-x64",
            Architecture.Arm64 when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx-arm64",
            _ => "unknown"
        };

    private static string GetNativeFileName() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "webrtc_sharp.dll" :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libwebrtc_sharp.so" :
        "libwebrtc_sharp.dylib";

    // Client lifecycle
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webrtc_sharp_create_client([MarshalAs(UnmanagedType.LPUTF8Str)] string? configJson);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webrtc_sharp_destroy_client(IntPtr handle);

    // SDP negotiation
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webrtc_sharp_create_offer(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webrtc_sharp_set_remote_offer(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId, [MarshalAs(UnmanagedType.LPUTF8Str)] string sdp);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int webrtc_sharp_set_remote_answer(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId, [MarshalAs(UnmanagedType.LPUTF8Str)] string sdp);

    // ICE candidates
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int webrtc_sharp_add_ice_candidate(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId, [MarshalAs(UnmanagedType.LPUTF8Str)] string candidate, [MarshalAs(UnmanagedType.LPUTF8Str)] string sdpMid, int sdpMlineIndex);

    // Messaging
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int webrtc_sharp_send_message(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId, byte[] data, nuint len);

    // Events
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webrtc_sharp_poll_events(IntPtr handle);

    // Connection state
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int webrtc_sharp_is_connected(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int webrtc_sharp_wait_for_ice_connected(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId);

    // Peer management
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int webrtc_sharp_close_peer(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string peerId);

    // Memory management
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void webrtc_sharp_free_string(IntPtr s);

    // Version
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr webrtc_sharp_version();
}
