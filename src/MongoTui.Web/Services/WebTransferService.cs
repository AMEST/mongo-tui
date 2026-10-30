using System.Text;
using System.Text.Json;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Collections;
using MongoTui.Domain.Exceptions;
using MongoTui.Domain.Transfer;

namespace MongoTui.Web.Services;

/// <summary>Uses private temporary files and bounds browser collection transfers.</summary>
public sealed class WebTransferService(ImportService imports, ExportService exports)
{
    public const long MaxBytes = 32 * 1024 * 1024;
    public const int MaximumExportDocuments = 10_000;
    private const int ExportSizeCheckIntervalMs = 250;

    public async Task ImportAsync(MongoConnectionTarget target, CollectionRef collection, Stream input,
        bool array, CancellationToken cancellationToken)
    {
        await using var file = CreateTemporaryFile();
        if (array) await WriteArrayAsync(input, file, cancellationToken);
        else await input.CopyToAsync(file, cancellationToken);
        if (file.Length > MaxBytes) throw new DomainException("The transfer exceeds the 32 MB size limit.");
        await file.FlushAsync(cancellationToken);
        await imports.ImportAsync(target, new ImportRequest
        { Collection = collection, InputPath = file.Name, Format = ImportFormat.Json }, cancellationToken);
    }

    public async Task<FileStream> ExportAsync(MongoConnectionTarget target, CollectionRef collection,
        string filter, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaximumExportDocuments);
        DocumentService.Parse(filter);
        var file = CreateTemporaryFile();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            var export = exports.ExportAsync(target, new ExportRequest
            { Collection = collection, OutputPath = file.Name, Format = ExportFormat.CanonicalJson, FilterJson = filter, Limit = limit }, operation.Token);
            while (!export.IsCompleted)
            {
                await Task.WhenAny(export, Task.Delay(ExportSizeCheckIntervalMs, CancellationToken.None));
                if (file.Length > MaxBytes) await operation.CancelAsync();
            }
            await export;
            if (file.Length > MaxBytes) throw new DomainException("The export exceeds the 32 MB size limit.");
            file.Position = 0;
            return file;
        }
        catch { await file.DisposeAsync(); throw; }
    }

    private static FileStream CreateTemporaryFile()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose,
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(Path.Combine(Path.GetTempPath(), $"mongotui-{Guid.NewGuid():N}.json"), options);
    }

    private static async Task WriteArrayAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(input, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new DomainException("Expected a JSON array of documents.");
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw new DomainException("Each imported value must be a document.");
            var json = BsonTextCodec.Format(BsonTextCodec.ParseDocument(item.GetRawText()), false);
            await output.WriteAsync(Encoding.UTF8.GetBytes(json + "\n"), cancellationToken);
        }
    }
}
