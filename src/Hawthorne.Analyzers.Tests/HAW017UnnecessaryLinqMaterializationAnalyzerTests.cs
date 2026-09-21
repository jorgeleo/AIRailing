using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW017UnnecessaryLinqMaterializationAnalyzerTests
{
    private static readonly MetadataReference LinqReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
    private static readonly MetadataReference RuntimeReference = MetadataReference.CreateFromFile(
        Path.Combine(Path.GetDirectoryName(typeof(Enumerable).Assembly.Location)!, "System.Runtime.dll"));
    private static readonly MetadataReference CollectionsReference = MetadataReference.CreateFromFile(
        Path.Combine(Path.GetDirectoryName(typeof(Enumerable).Assembly.Location)!, "System.Collections.dll"));
    private static readonly MetadataReference LinqExpressionsReference = MetadataReference.CreateFromFile(typeof(Expression).Assembly.Location);

    [Fact]
    public async Task Analyze_WhenToListImmediatelyFeedsWhere_ReportsHAW017()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                IEnumerable<int> Filter(IEnumerable<int> source) => source.ToList().Where(value => value > 0);
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference, LinqExpressionsReference);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW017", diagnostic.Id);
        Assert.Contains("ToList", diagnostic.GetMessage());
        Assert.Contains("Where", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenToArrayImmediatelyFeedsCount_ReportsHAW017()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count(IEnumerable<int> source) => source.ToArray().Count();
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference, LinqExpressionsReference);

        Assert.Equal("HAW017", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenMaterializationIsStored_DoesNotReportHAW017()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count(IEnumerable<int> source)
                {
                    var values = source.ToList();
                    return values.Count;
                }
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference, LinqExpressionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMaterializationFeedsListOnlyIndexing_DoesNotReportHAW017()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int First(IEnumerable<int> source) => source.ToList()[0];
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference, LinqExpressionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenToListAnalysisIsDisabled_DoesNotReportHAW017()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                IEnumerable<int> Filter(IEnumerable<int> source) => source.ToList().Where(value => value > 0);
            }
            """, """
            { "version": 1, "rules": { "HAW017": { "analyzeToList": false } } }
            """, LinqReference, RuntimeReference, CollectionsReference, LinqExpressionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW017()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Diagnostics.CodeAnalysis;
            using System.Linq;
            class Query
            {
                [SuppressMessage("Hawthorne.Reliability", "HAW017", Justification = "Provider requires materialization before translation boundary.")]
                IEnumerable<int> Filter(IEnumerable<int> source) => source.ToList().Where(value => value > 0);
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location), LinqReference, RuntimeReference, CollectionsReference, LinqExpressionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAnalyzeToArrayIsNotBoolean_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Query { }
            """, """
            { "version": 1, "rules": { "HAW017": { "analyzeToArray": "yes" } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
