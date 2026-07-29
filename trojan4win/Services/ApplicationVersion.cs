using System.Reflection;

namespace trojan4win.Services;

public static class ApplicationVersion
{
    public static string DisplayVersion { get; } = ResolveDisplayVersion();

    private static string ResolveDisplayVersion()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+', 2)[0];

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
