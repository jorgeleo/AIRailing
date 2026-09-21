using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Hawthorne.Analyzers.Tests;

public sealed class HAW013ExceptionLaunderingAnalyzerTests
{
    [Fact]
    public async Task Analyze_WhenCatchOnlyWrapsInApplicationException_ReportsHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host
            {
                void Save()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception)
                    {
                        throw new ApplicationException("Saving failed.", exception);
                    }
                }
            }
            """);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HAW013", diagnostic.Id);
        Assert.Contains("ApplicationException", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Analyze_WhenCatchAddsStructuredContext_DoesNotReportHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host
            {
                void Save(string customerId)
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception)
                    {
                        throw new ApplicationException(customerId, exception);
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenCatchRethrowsOriginalException_DoesNotReportHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host
            {
                void Save()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenCatchThrowsSpecificDomainException_DoesNotReportHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class SaveException : Exception
            {
                public SaveException(string message, Exception inner) : base(message, inner) { }
            }
            class Host
            {
                void Save()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception)
                    {
                        throw new SaveException("Saving failed.", exception);
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenLogAndRethrowArePresent_DoesNotReportHAW013ByDefault()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host
            {
                void Log(Exception exception) { }
                void Save()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception)
                    {
                        Log(exception);
                        throw;
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenLogAndRethrowAreEnabled_ReportsHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host
            {
                void LogError(Exception exception) { }
                void Save()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception)
                    {
                        LogError(exception);
                        throw;
                    }
                }
            }
            """, """
            { "version": 1, "rules": { "HAW013": { "reportLogAndRethrow": true } } }
            """);

        Assert.Equal("HAW013", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenConfiguredGenericExceptionTypeIsWrapped_ReportsHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            class Host
            {
                void Save()
                {
                    try { throw new Exception(); }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException("Saving failed.", exception);
                    }
                }
            }
            """, """
            { "version": 1, "rules": { "HAW013": { "genericExceptionTypes": ["System.InvalidOperationException"] } } }
            """);

        Assert.Equal("HAW013", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_WhenSuppressedWithJustification_DoesNotReportHAW013()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            using System;
            using System.Diagnostics.CodeAnalysis;
            class Host
            {
                [SuppressMessage("Hawthorne.Reliability", "HAW013", Justification = "Legacy API exposes only ApplicationException.")]
                void Save()
                {
                    try { throw new InvalidOperationException(); }
                    catch (Exception exception)
                    {
                        throw new ApplicationException("Saving failed.", exception);
                    }
                }
            }
            """, null, MetadataReference.CreateFromFile(typeof(SuppressMessageAttribute).Assembly.Location));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WhenGenericExceptionTypeListIsInvalid_ReportsHAW900()
    {
        var diagnostics = await AnalyzerTestHost.AnalyzeAsync("""
            class Host { }
            """, """
            { "version": 1, "rules": { "HAW013": { "genericExceptionTypes": [] } } }
            """);

        Assert.Equal("HAW900", Assert.Single(diagnostics).Id);
    }
}
