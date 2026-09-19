using Microsoft.CodeAnalysis;

namespace Hawthorne.Analyzers.Configuration;

internal sealed class HawthorneExceptionEvaluator(HawthorneConfiguration configuration)
{
    internal bool IsExcepted(string diagnosticId, SyntaxTree syntaxTree)
    {
        if (configuration.ProjectDirectory is null)
        {
            return false;
        }

        var relativePath = PathNormalizer.GetProjectRelativePath(configuration.ProjectDirectory, syntaxTree.FilePath);
        return relativePath is not null && configuration.Exceptions.Any(exception =>
            exception.File == relativePath && exception.Rules.Contains(diagnosticId));
    }
}
