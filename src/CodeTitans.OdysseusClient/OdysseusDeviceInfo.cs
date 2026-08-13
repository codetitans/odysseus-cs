using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CodeTitans.Odysseus;

/// <summary>
/// Captures a best-effort snapshot of app/device information, meant to be attached as event
/// data/context - typically once, to an "app start" event or log entry - so it's on hand later when
/// reading logs. Every individual piece is gathered defensively: a failure reading one thing (a
/// locked-down environment, a missing API, ...) never prevents the rest from being collected, and
/// never throws back at the caller.
/// </summary>
static class OdysseusDeviceInfo
{
    /// <summary>
    /// Captures information about this application's own assembly/process: name, version,
    /// debug/release build, and similar.
    /// </summary>
    public static Dictionary<string, object> CaptureAppInfo(Action<string>? internalLog = null)
    {
        var info = new Dictionary<string, object>();

        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var name = assembly.GetName();

            if (!string.IsNullOrEmpty(name.Name))
            {
                info["app_name"] = name.Name;
            }
            if (name.Version != null)
            {
                info["version"] = name.Version.ToString();
            }

            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(informationalVersion))
            {
                info["informational_version"] = informationalVersion!;
            }

            var debug = IsDebugBuild(assembly);
            info["debug"] = debug;
            info["build_type"] = debug ? "debug" : "release";
        }
        catch (Exception ex)
        {
            internalLog?.Invoke($"Failed to capture assembly info: {ex.Message}");
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            info["process_name"] = process.ProcessName;
            info["process_id"] = process.Id;
            info["start_time"] = process.StartTime.ToUniversalTime();
        }
        catch (Exception ex)
        {
            internalLog?.Invoke($"Failed to capture process info: {ex.Message}");
        }

        try
        {
            info["base_directory"] = AppContext.BaseDirectory;
        }
        catch (Exception ex)
        {
            internalLog?.Invoke($"Failed to capture base directory: {ex.Message}");
        }

        info["debugger_attached"] = Debugger.IsAttached;

        return info;
    }

    /// <summary>
    /// Captures information about the device/host and its current runtime state: OS/hardware
    /// identification, runtime, memory and locale.
    /// <para>
    /// The <c>"platform"</c> entry reflects the OS this process is currently running on:
    /// <c>".NET Core (Windows)"</c>, <c>".NET Core (Linux)"</c> or <c>".NET Core (macOS)"</c>.
    /// </para>
    /// </summary>
    public static Dictionary<string, object> CaptureDeviceInfo(Action<string>? internalLog = null)
    {
        var info = new Dictionary<string, object>
        {
            ["platform"] = ResolvePlatformName(),
            ["os_description"] = RuntimeInformation.OSDescription,
            ["os_architecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["framework_description"] = RuntimeInformation.FrameworkDescription,
            ["processor_count"] = Environment.ProcessorCount,
            ["is_64bit_os"] = Environment.Is64BitOperatingSystem,
            ["is_64bit_process"] = Environment.Is64BitProcess,
            ["locale"] = CultureInfo.CurrentCulture.Name,
            ["timezone"] = TimeZoneInfo.Local.Id,
        };

        try
        {
            info["machine_name"] = Environment.MachineName;
        }
        catch (Exception ex)
        {
            internalLog?.Invoke($"Failed to capture machine name: {ex.Message}");
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            info["working_set_bytes"] = process.WorkingSet64;
        }
        catch (Exception ex)
        {
            internalLog?.Invoke($"Failed to capture memory info: {ex.Message}");
        }

        try
        {
            info["gc_total_memory_bytes"] = GC.GetTotalMemory(false);
        }
        catch (Exception ex)
        {
            internalLog?.Invoke($"Failed to capture GC memory info: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// Resolves the ".NET Core (Windows|Linux|macOS)" platform label for the OS this process is
    /// currently running on.
    /// </summary>
    private static string ResolvePlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ".NET Core (Windows)";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ".NET Core (Linux)";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ".NET Core (macOS)";
        }

        return $".NET Core ({RuntimeInformation.OSDescription})";
    }

    // There is no reliable, portable "was this built in Release/Debug configuration" signal at
    // runtime - the JIT-tracking flag on DebuggableAttribute (emitted by the C# compiler based on
    // the build configuration) is the same best-effort heuristic used by most diagnostics tooling.
    private static bool IsDebugBuild(Assembly assembly)
    {
        #if DEBUG
            return true;
        #else
            var debuggable = assembly.GetCustomAttribute<DebuggableAttribute>();
            return debuggable != null && debuggable.IsJITTrackingEnabled;
        #endif
    }
}
