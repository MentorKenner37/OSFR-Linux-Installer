using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OSFR.Linux.Installer.Services;

public sealed record ProtonCompatibility(string RuntimeArchitecture, bool Compatible, string Message);

public sealed record ProtonCandidate(
    string Name,
    string Path,
    string? SteamRoot,
    string RuntimeArchitecture,
    bool Compatible,
    string CompatibilityMessage,
    bool Recommended = false);

public sealed record SystemState(
    bool IsLinux,
    bool IsX64,
    string? SteamRoot,
    string? ProtonPath,
    IReadOnlyList<ProtonCandidate>? ProtonCandidates = null,
    bool ProtonCompatible = false,
    string? ProtonCompatibilityMessage = null,
    string OsName = "Unknown",
    string KernelVersion = "Unknown",
    string CpuModel = "Unknown",
    string Memory = "Unknown",
    string Gpu = "Not detected",
    string Desktop = "Unknown",
    string SessionType = "Unknown")
{
    public bool Ready => IsLinux && IsX64 && SteamRoot is not null && ProtonPath is not null && ProtonCompatible;

    public SystemState WithProton(ProtonCandidate candidate) => this with
    {
        SteamRoot = candidate.SteamRoot ?? SteamRoot,
        ProtonPath = candidate.Path,
        ProtonCompatible = candidate.Compatible,
        ProtonCompatibilityMessage = candidate.CompatibilityMessage
    };
}

public static class SystemDetector
{
    private sealed record SteamLibraryContext(string LibraryPath, string SteamRoot);
    private sealed record CandidatePath(string ProtonPath, string? SteamRoot);

    public static SystemState Detect()
    {
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var isX64 = RuntimeInformation.OSArchitecture == Architecture.X64;
        var contexts = FindSteamLibraryContexts();
        var candidates = FindProtonCandidates(contexts);
        var selected = candidates.FirstOrDefault(candidate => candidate.Compatible);
        var fallbackSteamRoot = FindSteamRoots().FirstOrDefault(Directory.Exists);
        var steamRoot = selected?.SteamRoot ?? fallbackSteamRoot;

        var state = new SystemState(
            isLinux,
            isX64,
            steamRoot,
            selected?.Path,
            candidates,
            selected?.Compatible ?? false,
            selected?.CompatibilityMessage,
            DetectOsName(),
            DetectKernelVersion(),
            DetectCpuModel(),
            DetectMemory(),
            DetectGpu(),
            DetectDesktop(),
            DetectSessionType());

        InstallerLog.Info(
            $"System detection: Linux={state.IsLinux}, x64={state.IsX64}, OS={state.OsName}, Kernel={state.KernelVersion}, " +
            $"CPU={state.CpuModel}, RAM={state.Memory}, GPU={state.Gpu}, Desktop={state.Desktop}, Session={state.SessionType}, " +
            $"Steam={(state.SteamRoot ?? "not found")}, Proton={(state.ProtonPath ?? "not found")}, " +
            $"ProtonCompatible={state.ProtonCompatible}, ProtonCandidates={candidates.Count}");

        return state;
    }

    public static IEnumerable<string> FindSteamRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".steam", "debian-installation");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".steam", "root");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
    }

    public static IEnumerable<string> FindSteamLibraries() =>
        FindSteamLibraryContexts().Select(context => context.LibraryPath).Distinct(StringComparer.Ordinal);

    public static IEnumerable<string> ParseSteamLibraryPaths(string vdfText)
    {
        foreach (Match match in Regex.Matches(vdfText, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
        {
            var path = match.Groups["path"].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(path))
                yield return path;
        }
    }

    public static IReadOnlyList<ProtonCandidate> FindProtonCandidates(IEnumerable<string>? libraries = null)
    {
        if (libraries is null)
            return FindProtonCandidates(FindSteamLibraryContexts());

        var fallbackRoot = FindSteamRoots().FirstOrDefault(Directory.Exists);
        var contexts = libraries
            .Where(Directory.Exists)
            .Select(path => new SteamLibraryContext(
                CanonicalizePath(path),
                fallbackRoot is null ? CanonicalizePath(path) : CanonicalizePath(fallbackRoot)))
            .GroupBy(context => context.LibraryPath, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        return FindProtonCandidates(contexts);
    }

    public static ProtonCompatibility InspectProtonRuntime(string protonPath)
    {
        if (!File.Exists(protonPath))
            return new ProtonCompatibility("missing", false, "Proton launcher file is missing.");

        if (OperatingSystem.IsLinux())
        {
            try
            {
                if ((File.GetUnixFileMode(protonPath) & UnixFileMode.UserExecute) == 0)
                    return new ProtonCompatibility("unknown", false, "Proton launcher is not executable by the current user.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                InstallerLog.Warn($"Could not inspect execute permissions for {protonPath}: {ex.Message}");
            }
        }

        var name = GetProtonDirectoryName(protonPath);
        if (Regex.IsMatch(name, @"(?:aarch64|arm64)", RegexOptions.IgnoreCase))
        {
            var compatible = RuntimeInformation.OSArchitecture == Architecture.Arm64;
            return new ProtonCompatibility(
                "aarch64",
                compatible,
                compatible ? "ARM64 Proton build matches the host architecture." : "ARM64 Proton build is incompatible with this x86_64 system.");
        }

        var protonRoot = Path.GetDirectoryName(protonPath) ?? string.Empty;
        var runtimeBinaries = new[]
        {
            Path.Combine(protonRoot, "files", "bin", "wine64"),
            Path.Combine(protonRoot, "files", "bin", "wine"),
            Path.Combine(protonRoot, "dist", "bin", "wine64"),
            Path.Combine(protonRoot, "dist", "bin", "wine")
        };

        var detectedMachines = runtimeBinaries
            .Where(File.Exists)
            .Select(TryReadElfMachine)
            .Where(machine => machine is not null)
            .Select(machine => machine!.Value)
            .Distinct()
            .ToList();

        if (detectedMachines.Contains(62))
        {
            var compatible = RuntimeInformation.OSArchitecture == Architecture.X64;
            return new ProtonCompatibility(
                "x86_64",
                compatible,
                compatible ? "x86_64 Proton runtime verified." : "x86_64 Proton runtime does not match this host architecture.");
        }

        if (detectedMachines.Contains(183))
        {
            var compatible = RuntimeInformation.OSArchitecture == Architecture.Arm64;
            return new ProtonCompatibility(
                "aarch64",
                compatible,
                compatible ? "ARM64 Proton runtime verified." : "ARM64 Proton runtime is incompatible with this x86_64 system.");
        }

        if (Regex.IsMatch(name, @"(?:x86_64|amd64)", RegexOptions.IgnoreCase))
        {
            var compatible = RuntimeInformation.OSArchitecture == Architecture.X64;
            return new ProtonCompatibility(
                "x86_64",
                compatible,
                compatible ? "x86_64 Proton build matches the host architecture." : "x86_64 Proton build does not match this host architecture.");
        }

        return new ProtonCompatibility(
            "unknown",
            true,
            "Proton was found and is executable, but its runtime architecture could not be verified from the installed files.");
    }

    public static string CanonicalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            var info = Directory.Exists(fullPath)
                ? (FileSystemInfo)new DirectoryInfo(fullPath)
                : new FileInfo(fullPath);
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target is null ? fullPath : Path.GetFullPath(target.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            InstallerLog.Warn($"Could not resolve filesystem alias {fullPath}: {ex.Message}");
            return fullPath;
        }
    }

    private static List<SteamLibraryContext> FindSteamLibraryContexts()
    {
        var results = new List<SteamLibraryContext>();

        foreach (var discoveredRoot in FindSteamRoots().Where(Directory.Exists))
        {
            var root = CanonicalizePath(discoveredRoot);
            results.Add(new SteamLibraryContext(root, root));

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;

            try
            {
                foreach (var path in ParseSteamLibraryPaths(File.ReadAllText(vdf)))
                {
                    if (Directory.Exists(path))
                        results.Add(new SteamLibraryContext(CanonicalizePath(path), root));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                InstallerLog.Warn($"Could not read Steam library file {vdf}: {ex.Message}");
            }
        }

        return results
            .GroupBy(context => context.LibraryPath, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<ProtonCandidate> FindProtonCandidates(IReadOnlyList<SteamLibraryContext> contexts)
    {
        var candidatePaths = new List<CandidatePath>();

        foreach (var context in contexts)
        {
            AddSteamCommonCandidates(context.LibraryPath, context.SteamRoot, candidatePaths);
            AddCompatibilityToolCandidates(Path.Combine(context.LibraryPath, "compatibilitytools.d"), context.SteamRoot, candidatePaths);
        }

        var envToolPaths = Environment.GetEnvironmentVariable("STEAM_COMPAT_TOOL_PATHS");
        if (!string.IsNullOrWhiteSpace(envToolPaths))
        {
            var fallbackRoot = contexts.FirstOrDefault()?.SteamRoot ?? FindSteamRoots().FirstOrDefault(Directory.Exists);
            foreach (var path in envToolPaths.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                AddCompatibilityToolCandidates(path, fallbackRoot, candidatePaths);
        }

        var unique = candidatePaths
            .Where(candidate => File.Exists(candidate.ProtonPath))
            .Select(candidate => candidate with { ProtonPath = CanonicalizePath(candidate.ProtonPath) })
            .GroupBy(candidate => candidate.ProtonPath, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(candidate =>
            {
                var compatibility = InspectProtonRuntime(candidate.ProtonPath);
                return new ProtonCandidate(
                    GetProtonDirectoryName(candidate.ProtonPath),
                    candidate.ProtonPath,
                    candidate.SteamRoot,
                    compatibility.RuntimeArchitecture,
                    compatibility.Compatible,
                    compatibility.Message);
            })
            .OrderByDescending(candidate => candidate.Compatible)
            .ThenByDescending(candidate => IsExperimental(candidate.Path))
            .ThenByDescending(candidate => IsGeProton(candidate.Path))
            .ThenByDescending(candidate => GetProtonVersion(candidate.Path).Major)
            .ThenByDescending(candidate => GetProtonVersion(candidate.Path).Minor)
            .ThenByDescending(candidate => GetProtonVersion(candidate.Path).Patch)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recommendedIndex = unique.FindIndex(candidate => candidate.Compatible);
        return unique
            .Select((candidate, index) => candidate with { Recommended = index == recommendedIndex })
            .ToList();
    }

    private static ushort? TryReadElfMachine(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> header = stackalloc byte[20];
            if (stream.Read(header) < header.Length ||
                header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
                return null;

            var littleEndian = header[5] == 1;
            return littleEndian
                ? (ushort)(header[18] | (header[19] << 8))
                : (ushort)((header[18] << 8) | header[19]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not inspect Proton runtime binary {path}: {ex.Message}");
            return null;
        }
    }

    private static string DetectOsName()
    {
        if (!OperatingSystem.IsLinux())
            return RuntimeInformation.OSDescription;

        try
        {
            const string path = "/etc/os-release";
            if (File.Exists(path))
                return HostInfoParser.ParseOsRelease(File.ReadAllText(path)) ?? RuntimeInformation.OSDescription;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            InstallerLog.Warn($"Could not read Linux distribution information: {ex.Message}");
        }

        return RuntimeInformation.OSDescription;
    }

    private static string DetectKernelVersion()
    {
        try
        {
            const string path = "/proc/sys/kernel/osrelease";
            return File.Exists(path) ? File.ReadAllText(path).Trim() : Environment.OSVersion.VersionString;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read kernel version: {ex.Message}");
            return Environment.OSVersion.VersionString;
        }
    }

    private static string DetectCpuModel()
    {
        try
        {
            const string path = "/proc/cpuinfo";
            if (File.Exists(path))
                return HostInfoParser.ParseCpuInfo(File.ReadAllText(path)) ?? RuntimeInformation.ProcessArchitecture.ToString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read CPU model: {ex.Message}");
        }

        return RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static string DetectMemory()
    {
        try
        {
            const string path = "/proc/meminfo";
            if (File.Exists(path))
                return HostInfoParser.ParseMemory(File.ReadAllText(path)) ?? "Unknown";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read system memory: {ex.Message}");
        }

        return "Unknown";
    }

    private static string DetectGpu()
    {
        var lspci = TryRunCommand("lspci", "");
        if (!string.IsNullOrWhiteSpace(lspci))
        {
            var adapters = HostInfoParser.ParseLspciGraphics(lspci);
            if (adapters.Count > 0)
                return string.Join(" | ", adapters);
        }

        try
        {
            const string nvidiaRoot = "/proc/driver/nvidia/gpus";
            if (Directory.Exists(nvidiaRoot))
            {
                var models = new List<string>();
                foreach (var information in Directory.EnumerateFiles(nvidiaRoot, "information", SearchOption.AllDirectories))
                {
                    var modelLine = File.ReadLines(information)
                        .FirstOrDefault(line => line.StartsWith("Model:", StringComparison.OrdinalIgnoreCase));
                    if (modelLine is null)
                        continue;

                    var model = modelLine[(modelLine.IndexOf(':') + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(model))
                        models.Add(model);
                }

                if (models.Count > 0)
                    return string.Join(" | ", models.Distinct(StringComparer.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read NVIDIA GPU information: {ex.Message}");
        }

        return "Not detected (GPU model discovery is best-effort)";
    }

    private static string DetectDesktop()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (string.IsNullOrWhiteSpace(desktop))
            desktop = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        return string.IsNullOrWhiteSpace(desktop) ? "Unknown" : desktop;
    }

    private static string DetectSessionType()
    {
        var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return string.IsNullOrWhiteSpace(session) ? "Unknown" : session;
    }

    private static string? TryRunCommand(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(2000))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return null;
            }

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not run {fileName} for hardware detection: {ex.Message}");
            return null;
        }
    }

    private static void AddSteamCommonCandidates(string library, string? steamRoot, ICollection<CandidatePath> candidates)
    {
        var common = Path.Combine(library, "steamapps", "common");
        if (!Directory.Exists(common))
            return;

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(common))
            {
                var proton = Path.Combine(directory, "proton");
                if (File.Exists(proton))
                    candidates.Add(new CandidatePath(proton, steamRoot));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not enumerate Proton builds in {common}: {ex.Message}");
        }
    }

    private static void AddCompatibilityToolCandidates(string tools, string? steamRoot, ICollection<CandidatePath> candidates)
    {
        if (!Directory.Exists(tools))
            return;

        try
        {
            var directProton = Path.Combine(tools, "proton");
            if (File.Exists(directProton))
                candidates.Add(new CandidatePath(directProton, steamRoot));

            foreach (var directory in Directory.EnumerateDirectories(tools))
            {
                var proton = Path.Combine(directory, "proton");
                if (File.Exists(proton))
                    candidates.Add(new CandidatePath(proton, steamRoot));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not enumerate compatibility tools in {tools}: {ex.Message}");
        }
    }

    private static bool IsExperimental(string protonPath) =>
        GetProtonDirectoryName(protonPath).Contains("Experimental", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeProton(string protonPath) =>
        GetProtonDirectoryName(protonPath).Contains("GE-Proton", StringComparison.OrdinalIgnoreCase);

    private static (int Major, int Minor, int Patch) GetProtonVersion(string protonPath)
    {
        var name = GetProtonDirectoryName(protonPath);
        var match = Regex.Match(name, @"(?:GE-Proton|Proton\s*-?\s*)(?<major>\d+)(?:[.-](?<minor>\d+))?(?:[.-](?<patch>\d+))?", RegexOptions.IgnoreCase);
        if (!match.Success)
            return (0, 0, 0);

        _ = int.TryParse(match.Groups["major"].Value, out var major);
        _ = int.TryParse(match.Groups["minor"].Value, out var minor);
        _ = int.TryParse(match.Groups["patch"].Value, out var patch);
        return (major, minor, patch);
    }

    private static string GetProtonDirectoryName(string protonPath) =>
        Path.GetFileName(Path.GetDirectoryName(protonPath)) ?? string.Empty;
}
