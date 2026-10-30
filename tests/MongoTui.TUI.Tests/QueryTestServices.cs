using Microsoft.Extensions.Logging.Abstractions;
using MongoTui.Application.Abstractions.Diagnostics;
using FakeItEasy;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Services;

namespace MongoTui.TUI.Tests;

internal static class QueryTestServices
{
    public static QueryService Create(IMongoShellSessionFactory factory, IQueryHistoryRepository history) =>
        new(factory, history, A.Fake<IMongoGateway>(), new SettingsService(A.Fake<ISettingsStore>()), NullLogger<QueryService>.Instance);
}
