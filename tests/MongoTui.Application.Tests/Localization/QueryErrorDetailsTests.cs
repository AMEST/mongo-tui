using AwesomeAssertions;
using MongoTui.Application.Localization;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Tests.Localization;

public sealed class QueryErrorDetailsTests
{
    [Theory]
    [InlineData("Query failed: {0}", "Query failed: FormatException: Invalid JSON at line 2.")]
    [InlineData("Ошибка запроса: {0}", "Ошибка запроса: FormatException: Invalid JSON at line 2.")]
    public void Should_ShowUnderlyingCause_WhenQueryExceptionWrapsFailure(string template, string expected)
    {
        var failure = new QueryExecutionException("Query execution failed.", new FormatException("Invalid JSON at line 2."));

        var message = QueryErrorDetails.GetMessage(failure, template);

        message.Should().Be(expected);
    }

    [Fact]
    public void Should_RedactUrisAndKeepReason_WhenServerDiagnosticContainsCredentials()
    {
        var failure = new QueryExecutionException("Cannot connect to mongodb+srv://user:secret@host/db?password=secret: access denied");

        var message = QueryErrorDetails.GetMessage(failure, "Failed: {0}");

        message.Should().Contain("access denied").And.Contain("[redacted]")
            .And.NotContain("secret").And.NotContain("user").And.NotContain("host");
    }

    [Fact]
    public void Should_LimitDiagnosticLength_WhenExceptionContainsLargeOutput()
    {
        var failure = new InvalidOperationException(new string('x', 5000));

        var message = QueryErrorDetails.GetMessage(failure, "Failed: {0}");

        message.Length.Should().BeLessThanOrEqualTo(1501);
        message.Should().StartWith("Failed: InvalidOperationException:").And.EndWith("…");
    }
}
