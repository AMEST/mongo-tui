using AwesomeAssertions;
using MongoTui.Application.Diagnostics;

namespace MongoTui.Application.Tests.Services;

public sealed class ExceptionDiagnosticsTests
{
    [Fact]
    public void Should_KeepTypesAndStackWithoutMessages_When_ExceptionContainsSecrets()
    {
        var error = Record.Exception((Action)(() => throw new AggregateException("mongodb://user:secret@host",
            new InvalidOperationException("password-document-value"), new IOException("private-path"))))!;
        error.Data["password"] = "private-data";

        var description = ExceptionDiagnostics.Describe(error);

        description.Should().Contain(nameof(AggregateException)).And.Contain(nameof(InvalidOperationException))
            .And.Contain(nameof(IOException)).And.Contain(nameof(ExceptionDiagnosticsTests))
            .And.NotContain("mongodb://").And.NotContain("password-document-value")
            .And.NotContain("private-path").And.NotContain("private-data");
    }
}
