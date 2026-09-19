namespace Hawthorne.Analyzers.Configuration;

internal static class PathNormalizer
{
    internal static string GetFileName(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        var separator = normalizedPath.LastIndexOf('/');
        return separator >= 0 ? normalizedPath.Substring(separator + 1) : normalizedPath;
    }

    internal static bool TryNormalizeProjectRelativePath(string path, out string normalizedPath)
    {
        normalizedPath = path.Replace('\\', '/');
        while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath.Substring(2);
        }

        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
            normalizedPath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            normalizedPath = string.Empty;
            return false;
        }

        return true;
    }

    internal static string? GetProjectRelativePath(string projectDirectory, string sourceFilePath)
    {
        var normalizedProjectDirectory = projectDirectory.Replace('\\', '/').TrimEnd('/');
        var normalizedSourceFilePath = sourceFilePath.Replace('\\', '/');
        var prefix = normalizedProjectDirectory + "/";
        if (!normalizedSourceFilePath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return TryNormalizeProjectRelativePath(normalizedSourceFilePath.Substring(prefix.Length), out var relativePath)
            ? relativePath
            : null;
    }

    internal static string? GetDirectory(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        var separator = normalizedPath.LastIndexOf('/');
        return separator > 0 ? normalizedPath.Substring(0, separator) : null;
    }
}
