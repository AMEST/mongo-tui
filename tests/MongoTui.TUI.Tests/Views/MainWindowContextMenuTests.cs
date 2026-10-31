using System.Drawing;
using System.Reflection;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Models;
using MongoTui.Domain.Connections;
using MongoTui.Domain.Primitives;
using MongoTui.TUI.ViewModels;
using MongoTui.TUI.Views;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class MainWindowContextMenuTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("en", true)]
    [InlineData("ru", true)]
    public async Task Should_OpenDatabaseQueryOnce_WhenContextItemAcceptedWithKeyboard(string language, bool space)
    {
        await using var host = new TuiFeatureTestHost(language);
        SelectDatabase(host);
        var timedOut = false;
        host.App.AddTimeout(TimeSpan.Zero, () =>
        {
            typeof(MainWindow).GetMethod("OpenTreeContextMenu", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(host.Window, [Point.Empty]);
            host.App.RequestStop(host.Window);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            host.App.Keyboard.RaiseKeyDownEvent(space ? new Key(' ') : Key.Enter);
            return false;
        });
        host.App.AddTimeout(TimeSpan.FromSeconds(2), () => { timedOut = true; host.App.RequestStop(); return false; });

        host.App.Run(host.Window);

        timedOut.Should().BeFalse();
        host.Tabs.TabCount.Should().Be(1);
        host.Tabs.ActiveTab!.Database.Should().Be("app");
        host.Tabs.ActiveTab.Collection.Should().BeNull();
        A.CallTo(() => host.Session.EvaluateAsync(A<string>._, A<string?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static void SelectDatabase(TuiFeatureTestHost host)
    {
        var profile = ConnectionProfile.Create(ConnectionId.New(), "local", "mongodb://localhost");
        var tree = Field<TreeViewModel>(host.Window, "_tree");
        tree.SetProfiles([profile]);
        tree.AddOrReplace(new ActiveConnection(profile, new MongoConnectionTarget(profile.Uri), [new DatabaseInfo("app", false)]));
        tree.ExpandConnection(profile.Id);
        typeof(MainWindow).GetMethod("RebuildTree", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(host.Window, [-1]);
        var rows = Field<List<TreeRow>>(host.Window, "_rows");
        Field<ListView>(host.Window, "_treeList").SelectedItem = rows.FindIndex(row => row.Kind == TreeRowKind.Database);
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
}
