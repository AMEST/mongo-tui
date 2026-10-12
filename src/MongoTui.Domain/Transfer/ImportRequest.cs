using MongoTui.Domain.Collections;

namespace MongoTui.Domain.Transfer;

/// <summary>Describes an import operation.</summary>
public sealed record ImportRequest
{
    public required CollectionRef Collection { get; init; }

    public required string InputPath { get; init; }

    public required ImportFormat Format { get; init; }

    public ImportMode Mode { get; init; } = ImportMode.Insert;

    public bool DropCollection { get; init; }
}
