using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW006PrematureGeneralizationAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenAbstractBaseHasOneConcreteSourceDerivedType_ReportsHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor<TInput, TOutput>
            {
                public abstract TOutput Process(TInput input);
            }
            sealed class CustomerProcessor : Processor<Customer, CustomerResult>
            {
                public override CustomerResult Process(Customer input) => new CustomerResult();
            }
            sealed class Customer { }
            sealed class CustomerResult { }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW006", diagnostic.Id);
        Assert.Contains("Processor", diagnostic.GetMessage());
        Assert.Contains("derived type", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenAbstractBaseHasTwoConcreteSourceDerivedTypes_DoesNotReportHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor { public abstract int Process(int value); }
            sealed class CustomerProcessor : Processor { public override int Process(int value) => value; }
            sealed class OrderProcessor : Processor { public override int Process(int value) => value; }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAbstractBaseHasSignificantSharedBehavior_DoesNotReportHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor
            {
                protected int Normalize(int value)
                {
                    var result = value + 1;
                    if (result < 0) return 0;
                    return result;
                }
                public abstract int Process(int value);
            }
            sealed class CustomerProcessor : Processor { public override int Process(int value) => Normalize(value); }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenAbstractBaseHasSharedMutableState_DoesNotReportHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor
            {
                private int attempts;
                public abstract int Process(int value);
            }
            sealed class CustomerProcessor : Processor { public override int Process(int value) => value; }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSourceGenericAbstractTypeHasOneClosedConstruction_ReportsHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor<T> { public abstract T Process(T value); }
            sealed class Host { private Processor<Customer> processor = null!; }
            sealed class Customer { }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW006", diagnostic.Id);
        Assert.Contains("closed generic construction", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenSourceGenericAbstractTypeHasTwoClosedConstructions_DoesNotReportHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor<T> { public abstract T Process(T value); }
            sealed class Host
            {
                private Processor<Customer> customers = null!;
                private Processor<Order> orders = null!;
            }
            sealed class Customer { }
            sealed class Order { }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenGenericConstructionAnalysisIsDisabled_DoesNotReportHAW006FromThatEvidence()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor<T> { public abstract T Process(T value); }
            sealed class Host { private Processor<Customer> processor = null!; }
            sealed class Customer { }
            """, """
            { "version": 1, "rules": { "HAW006": { "analyzeSingleClosedGenericUse": false } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSharedStatementLimitIsRaised_ReportsHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor
            {
                protected int Normalize(int value)
                {
                    var result = value + 1;
                    if (result < 0) return 0;
                    return result;
                }
                public abstract int Process(int value);
            }
            sealed class CustomerProcessor : Processor { public override int Process(int value) => Normalize(value); }
            """, """
            { "version": 1, "rules": { "HAW006": { "maximumSharedExecutableStatements": 4 } } }
            """);

        Assert.Equal("HAW006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW006()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            [SuppressMessage("Hawthorne.Architecture", "HAW006", Justification = "Public plugin contract remains stable.")]
            abstract class Processor { public abstract int Process(int value); }
            sealed class CustomerProcessor : Processor { public override int Process(int value) => value; }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSharedStatementLimitIsNotPositive_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            abstract class Processor { public abstract int Process(int value); }
            """, """
            { "version": 1, "rules": { "HAW006": { "maximumSharedExecutableStatements": 0 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
