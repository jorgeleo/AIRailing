using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW018RepeatedEnumerationAnalyzerTests
{
    private static readonly MetadataReference LinqReference = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
    private static readonly MetadataReference RuntimeReference = MetadataReference.CreateFromFile(
        Path.Combine(Path.GetDirectoryName(typeof(Enumerable).Assembly.Location)!, "System.Runtime.dll"));
    private static readonly MetadataReference CollectionsReference = MetadataReference.CreateFromFile(
        Path.Combine(Path.GetDirectoryName(typeof(Enumerable).Assembly.Location)!, "System.Collections.dll"));

    [Fact]
    public async Task Analyze_WhenEnumerableParameterIsConsumedTwice_ReportsHAW018()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count(IEnumerable<int> source)
                {
                    var count = source.Count();
                    return source.Any() ? count : 0;
                }
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW018", diagnostic.Id);
        Assert.Contains("source", diagnostic.GetMessage());
        Assert.Contains("2", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenEnumerableLocalIsConsumedTwice_ReportsHAW018()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count()
                {
                    IEnumerable<int> values = GetValues();
                    var count = values.Count();
                    return values.Any() ? count : 0;
                }
                IEnumerable<int> GetValues() { yield return 1; }
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference);

        Assert.Equal("HAW018", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenCollectionParameterIsConsumedTwice_DoesNotReportHAW018()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count(IReadOnlyCollection<int> source)
                {
                    var count = source.Count();
                    return source.Any() ? count : 0;
                }
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenEnumerableIsReassignedToAList_DoesNotReportHAW018()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count(IEnumerable<int> source)
                {
                    var count = source.Count();
                    source = source.ToList();
                    return source.Any() ? count : 0;
                }
            }
            """, null, LinqReference, RuntimeReference, CollectionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMinimumEnumerationCountIsRaised_DoesNotReportHAW018()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Collections.Generic;
            using System.Linq;
            class Query
            {
                int Count(IEnumerable<int> source)
                {
                    var count = source.Count();
                    return source.Any() ? count : 0;
                }
            }
            """, """
            { "version": 1, "rules": { "HAW018": { "minimumEnumerations": 3 } } }
            """, LinqReference, RuntimeReference, CollectionsReference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMinimumEnumerationCountIsNotPositive_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Query { }
            """, """
            { "version": 1, "rules": { "HAW018": { "minimumEnumerations": 0 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
