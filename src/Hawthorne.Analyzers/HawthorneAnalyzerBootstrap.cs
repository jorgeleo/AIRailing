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
        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var configuration = HawthorneConfigurationLoader.Load(compilationStartContext.Options.AdditionalFiles);
            if (!configuration.IsValid)
            {
                compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
                    compilationEndContext.ReportDiagnostic(Diagnostic.Create(
                        HawthorneDiagnosticDescriptors.HAW900,
                        GetConfigurationLocation(configuration.Source),
                        configuration.ErrorMessage)));
                return;
            }

            var hawthorneConfiguration = configuration.Configuration!;
            HAW105MethodLengthAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW103NestingAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW101CyclomaticComplexityAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
            {
                foreach (var exception in hawthorneConfiguration.Exceptions)
                {
                    var isMatched = compilationEndContext.Compilation.SyntaxTrees.Any(syntaxTree =>
                        PathNormalizer.GetProjectRelativePath(hawthorneConfiguration.ProjectDirectory!, syntaxTree.FilePath) == exception.File);
                    if (!isMatched)
                    {
                        compilationEndContext.ReportDiagnostic(Diagnostic.Create(
                        HawthorneDiagnosticDescriptors.HAW900,
                        GetConfigurationLocation(configuration.Source),
                        $"Exception path '{exception.File}' does not match a source file in this compilation."));
                    }
                }
            });
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
