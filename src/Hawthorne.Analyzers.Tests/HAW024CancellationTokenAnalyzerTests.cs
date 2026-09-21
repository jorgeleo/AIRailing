using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW024CancellationTokenAnalyzerTests
{
    private static readonly MetadataReference CancellationTokenReference =
        MetadataReference.CreateFromFile(typeof(CancellationToken).Assembly.Location);

    [Fact]
    public async Task Analyze_WhenTokenIsUnused_ReportsHAW024AtTheParameter()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            class Host
            {
                void Execute(CancellationToken cancellationToken) { var value = 1; }
            }
            """, null, CancellationTokenReference);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW024", diagnostic.Id);
        Assert.Contains("cancellationToken", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenTokenIsChecked_DoesNotReportHAW024()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            class Host
            {
                void Execute(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            """, null, CancellationTokenReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTokenIsForwarded_DoesNotReportHAW024()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            class Repository { public void Save(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); } }
            class Host
            {
                private readonly Repository repository = new Repository();
                void Execute(CancellationToken cancellationToken) => repository.Save(cancellationToken);
            }
            """, """
            { "version": 1, "rules": { "HAW003": { "enabled": false } } }
            """, CancellationTokenReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenCancellableCallOmitsAvailableToken_ReportsHAW024AtTheCall()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            class Repository { public void Save(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); } }
            class Host
            {
                private readonly Repository repository = new Repository();
                void Execute(CancellationToken cancellationToken) => repository.Save();
            }
            """, null, CancellationTokenReference);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW024", diagnostic.Id);
        Assert.Contains("Save", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenCancellableCallUsesCancellationTokenNone_ReportsHAW024ByDefault()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            class Repository { public void Save(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); } }
            class Host
            {
                private readonly Repository repository = new Repository();
                void Execute(CancellationToken cancellationToken) => repository.Save(CancellationToken.None);
            }
            """, null, CancellationTokenReference);

        Assert.Equal("HAW024", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMissingForwardingIsDisabled_DoesNotReportTheCall()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            class Repository { public void Save(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); } }
            class Host
            {
                private readonly Repository repository = new Repository();
                void Execute(CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    repository.Save();
                }
            }
            """, """
            { "version": 1, "rules": { "HAW024": { "reportMissingForwarding": false } } }
            """, CancellationTokenReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTokenImplementationIsAnInterfaceContract_DoesNotReportHAW024ByDefault()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Threading;
            interface IHost { void Execute(CancellationToken cancellationToken); }
            class Host : IHost
            {
                public void Execute(CancellationToken cancellationToken) { }
            }
            """, """
            { "version": 1, "rules": { "HAW001": { "enabled": false } } }
            """, CancellationTokenReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW024()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            using System.Threading;
            class Host
            {
                [SuppressMessage("Hawthorne.Reliability", "HAW024", Justification = "Legacy callback cannot propagate cancellation.")]
                void Execute(CancellationToken cancellationToken) { }
            }
            """, null,
            CancellationTokenReference,
            MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTreatNoneAsMissingForwardingIsNotBoolean_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host { }
            """, """
            { "version": 1, "rules": { "HAW024": { "treatNoneAsMissingForwarding": 1 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
