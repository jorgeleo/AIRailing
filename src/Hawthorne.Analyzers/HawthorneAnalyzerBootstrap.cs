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
            HAW106LineBreakAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW103NestingAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW101CyclomaticComplexityAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW102CognitiveComplexityAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW004SingletonAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW002TrivialFactoryAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW003PassThroughAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW104CouplingAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW001SingleImplementationAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
            HAW901SuppressionAnalyzer.Register(compilationStartContext, hawthorneConfiguration);
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
