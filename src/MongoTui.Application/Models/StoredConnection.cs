using MongoTui.Domain.Connections;

namespace MongoTui.Application.Models;

/// <summary>A connection profile together with its optional encrypted secret.</summary>
public sealed record StoredConnection(ConnectionProfile Profile, EncryptedSecret? Secret);
