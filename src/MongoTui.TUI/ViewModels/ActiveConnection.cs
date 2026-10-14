using MongoTui.Application.Models;
using MongoTui.Application.Abstractions.Ssh;
using MongoTui.Domain.Connections;

namespace MongoTui.TUI.ViewModels;

/// <summary>An opened (active) connection shown in the tree, with its databases.</summary>
public sealed record ActiveConnection(
    ConnectionProfile Profile,
    MongoConnectionTarget Target,
    IReadOnlyList<DatabaseInfo> Databases,
    ISshTunnelSession? Tunnel = null);
