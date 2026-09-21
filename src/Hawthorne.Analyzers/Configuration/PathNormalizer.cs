namespace Hawthorne.Analyzers.Configuration;

internal static class PathNormalizer
{
    internal static string GetFileName(string path)
    {
        var normalizedPath = path.Replace('\\', '/');
        var separator = normalizedPath.LastIndexOf('/');
        return separator >= 0 ? normalizedPath.Substring(separator + 1) : normalizedPath;
    }

}
