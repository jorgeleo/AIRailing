using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW010OneMethodServiceAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenSmallServiceHasOnePublicMethodAndOneCaller_ReportsHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CreateCustomerService
            {
                public int Execute(int value) => value;
            }
            class Caller
            {
                int Create() => new CreateCustomerService().Execute(1);
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW010", diagnostic.Id);
        Assert.Contains("CreateCustomerService", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenServiceMethodHasTwoSourceCallers_DoesNotReportHAW010ByDefault()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CreateCustomerService
            {
                public int Execute(int value) => value;
            }
            class CallerOne { int Create() => new CreateCustomerService().Execute(1); }
            class CallerTwo { int Create() => new CreateCustomerService().Execute(2); }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenTypeNameIsOnlyTheGenericRole_DoesNotReportHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Service
            {
                public int Execute(int value) => value;
            }
            class Caller
            {
                int Create() => new Service().Execute(1);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredCallerLimitIsMet_ReportsHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CreateCustomerService
            {
                public int Execute(int value) => value;
            }
            class CallerOne { int Create() => new CreateCustomerService().Execute(1); }
            class CallerTwo { int Create() => new CreateCustomerService().Execute(2); }
            """, """
            { "version": 1, "rules": { "HAW010": { "maximumSourceCallers": 2 } } }
            """);

        Assert.Equal("HAW010", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenServiceHasAdditionalPublicBehavior_DoesNotReportHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CreateCustomerService
            {
                public int Execute(int value) => value;
                public int Preview(int value) => value;
            }
            class Caller
            {
                int Create() => new CreateCustomerService().Execute(1);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenServiceImplementsAnInterface_DoesNotReportHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            interface ICreateCustomer { int Execute(int value); }
            class CreateCustomerService : ICreateCustomer
            {
                public int Execute(int value) => value;
            }
            class Caller
            {
                int Create() => new CreateCustomerService().Execute(1);
            }
            """, """
            { "version": 1, "rules": { "HAW001": { "enabled": false } } }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenServiceMethodIsTooLarge_DoesNotReportHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class CreateCustomerService
            {
                public int Execute(int value)
                {

























                    return value;
                }
            }
            class Caller
            {
                int Create() => new CreateCustomerService().Execute(1);
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW010()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System.Diagnostics.CodeAnalysis;
            [SuppressMessage("Hawthorne.Architecture", "HAW010", Justification = "Command dispatcher registration boundary.")]
            class CreateCustomerService
            {
                public int Execute(int value) => value;
            }
            class Caller
            {
                int Create() => new CreateCustomerService().Execute(1);
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenMaximumSourceCallersIsNotPositive_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host { }
            """, """
            { "version": 1, "rules": { "HAW010": { "maximumSourceCallers": 0 } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
