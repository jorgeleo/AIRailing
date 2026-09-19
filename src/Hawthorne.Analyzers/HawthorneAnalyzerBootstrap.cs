using System.Collections.Immutable;
using Hawthorne.Analyzers.Configuration;
using Hawthorne.Analyzers.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Hawthorne.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HawthorneAnalyzerBootstrap : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => HawthorneDiagnosticDescriptors.All;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(compilationContext =>
        {
            var configuration = HawthorneConfigurationLoader.Load(compilationContext.Options.AdditionalFiles);
            if (!configuration.IsValid)
            {
                compilationContext.ReportDiagnostic(Diagnostic.Create(
                    HawthorneDiagnosticDescriptors.HAW900,
                    GetConfigurationLocation(configuration.Source),
                    configuration.ErrorMessage));
                return;
            }

            foreach (var exception in configuration.Configuration!.Exceptions)
            {
                var isMatched = compilationContext.Compilation.SyntaxTrees.Any(syntaxTree =>
                    PathNormalizer.GetProjectRelativePath(configuration.Configuration.ProjectDirectory!, syntaxTree.FilePath) == exception.File);
                if (!isMatched)
                {
                    compilationContext.ReportDiagnostic(Diagnostic.Create(
                        HawthorneDiagnosticDescriptors.HAW900,
                        GetConfigurationLocation(configuration.Source),
                        $"Exception path '{exception.File}' does not match a source file in this compilation."));
                }
            }
        });
    }

    private static Location GetConfigurationLocation(AdditionalText? configurationFile)
    {
        if (configurationFile is null)
        {
            return Location.None;
        }

        var start = new LinePosition(0, 0);
        return Location.Create(configurationFile.Path, new TextSpan(0, 0), new LinePositionSpan(start, start));
    }
}
