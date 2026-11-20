using AwesomeAssertions;
using FakeItEasy;
using MongoDB.Bson;
using MongoTui.Application.Abstractions.Mongo;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Models;
using MongoTui.Application.Services;
using MongoTui.Domain.Diagnostics;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Views;

#pragma warning disable CS0618
namespace MongoTui.TUI.Tests.Views;

public sealed class ExplainDialogEditingTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task Should_RunEditedQuery_WhenUnsupportedScriptIsCorrectedInDialog(string language)
    {
        var gateway = A.Fake<IMongoDiagnosticsGateway>(); var settings = A.Fake<ISettingsStore>();
        A.CallTo(() => gateway.ExplainAsync(A<MongoConnectionTarget>._, A<ExplainRequest>._, A<CancellationToken>._))
            .Returns(BsonDocument.Parse("{queryPlanner:{winningPlan:{stage:'IXSCAN'}}}"));
        var service = new ExplainService(gateway, new SettingsService(settings), new DiagnosticsSettingsService(settings));
        var app = A.Fake<IApplication>(); A.CallTo(() => app.MainThreadId).Returns(Environment.CurrentManagedThreadId);
        var localizer = new ResxLocalizer(); localizer.SetLanguage(language);
        var context = new QueryExecutionContext { Target = new("mongodb://localhost"), ConnectionId = ConnectionId.New(),
            TabId = Guid.NewGuid(), Database = "shop", Script = "const value = 1; db.orders.find({value})" };
        using var dialog = new ExplainDialog(service, context, localizer, app);
        await dialog.LoadAsync();
        dialog.SubViews.OfType<Label>().Should().Contain(label => label.Text == localizer.Get("Explain.Unsupported"));
        var editor = dialog.SubViews.OfType<TextView>().Single(view => !view.ReadOnly);
        editor.Text = "db.customers.find({active:true}).limit(5).toArray()";

        await dialog.LoadAsync();

        A.CallTo(() => gateway.ExplainAsync(context.Target, A<ExplainRequest>.That.Matches(request =>
            request.Collection.Collection.Value == "customers" && request.Limit == 5 && request.Filter!["active"].AsBoolean), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        editor.ReadOnly.Should().BeFalse(); context.Script.Should().StartWith("const value");
        dialog.SubViews.OfType<TextView>().Where(view => view.ReadOnly).Should().Contain(view => view.Text.Contains("IXSCAN", StringComparison.Ordinal));
    }
}
