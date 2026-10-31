using AwesomeAssertions;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Primitives;
using MongoTui.Domain.Transfer;

namespace MongoTui.Application.Tests.Services;

public sealed class TransferServiceTests
{
    private readonly IMongoTransferGateway _transfer = A.Fake<IMongoTransferGateway>();
    private readonly ExportService _export;
    private readonly ImportService _import;

    private static readonly MongoConnectionTarget Target = new("mongodb://localhost:27017");
    private static readonly CollectionRef Collection =
        new(DatabaseName.Create("app"), CollectionName.Create("users"));

    public TransferServiceTests()
    {
        _export = new ExportService(_transfer, NullLogger<ExportService>.Instance);
        _import = new ImportService(_transfer);
    }

    [Fact]
    public async Task Should_Export_When_OutputPathProvided()
    {
        var request = new ExportRequest
        {
            Collection = Collection,
            OutputPath = "/tmp/out.json",
            Format = ExportFormat.Json,
        };

        await _export.ExportAsync(Target, request, CancellationToken.None);

        A.CallTo(() => _transfer.ExportAsync(Target, request, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_OutputPathEmpty()
    {
        var request = new ExportRequest
        {
            Collection = Collection,
            OutputPath = "  ",
            Format = ExportFormat.Json,
        };

        var act = async () => await _export.ExportAsync(Target, request, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Should_Import_When_InputPathProvided()
    {
        var request = new ImportRequest
        {
            Collection = Collection,
            InputPath = "/tmp/in.json",
            Format = ImportFormat.Json,
        };

        await _import.ImportAsync(Target, request, CancellationToken.None);

        A.CallTo(() => _transfer.ImportAsync(Target, request, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ShouldThrow_DomainException_When_InputPathEmpty()
    {
        var request = new ImportRequest
        {
            Collection = Collection,
            InputPath = string.Empty,
            Format = ImportFormat.Json,
        };

        var act = async () => await _import.ImportAsync(Target, request, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
