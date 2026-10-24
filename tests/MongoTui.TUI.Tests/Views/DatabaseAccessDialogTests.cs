using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Domain.Users;
using MongoTui.TUI.Localization;
using MongoTui.TUI.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace MongoTui.TUI.Tests.Views;

public sealed class DatabaseAccessDialogTests
{
    private readonly IApplication _application = A.Fake<IApplication>();
    private readonly ResxLocalizer _localizer = new();

    [Theory]
    [InlineData(false, 0, DatabaseAccessLevel.Read)]
    [InlineData(true, 1, DatabaseAccessLevel.ReadWrite)]
    [InlineData(true, 2, DatabaseAccessLevel.DbOwner)]
    public void Should_AddTypedDatabaseAccess_WhenNameAbsentFromKnownDatabases(
        bool hasKnownDatabases, int selectedLevel, DatabaseAccessLevel expectedLevel)
    {
        using var dialog = new DatabaseAccessDialog(_localizer, _application, hasKnownDatabases ? ["orders"] : []);
        dialog.BeginInit();
        dialog.EndInit();
        dialog.SubViews.OfType<ListView>().Single().SelectedItem = selectedLevel;

        TypeAndAccept(dialog, "future_db");

        dialog.SelectedAccess.Should().NotBeNull();
        dialog.SelectedAccess!.Database.Value.Should().Be("future_db");
        dialog.SelectedAccess.Level.Should().Be(expectedLevel);
        A.CallTo(() => _application.RequestStop(dialog)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("en", "")]
    [InlineData("ru", "")]
    [InlineData("en", "bad.name")]
    [InlineData("ru", "bad.name")]
    public void Should_ShowValidationAndKeepDialogOpen_WhenTypedNameInvalid(string language, string name)
    {
        _localizer.SetLanguage(language);
        using var dialog = new DatabaseAccessDialog(_localizer, _application, []);
        dialog.BeginInit();
        dialog.EndInit();

        TypeAndAccept(dialog, name);

        dialog.SelectedAccess.Should().BeNull();
        dialog.SubViews.OfType<Label>().Last().Text.Should().Be(_localizer.Get("Users.Access.DatabaseInvalid"));
        A.CallTo(() => _application.RequestStop(dialog)).MustNotHaveHappened();
    }

    private void TypeAndAccept(DatabaseAccessDialog dialog, string name)
    {
        var field = dialog.SubViews.OfType<DropDownList>().Single();
        field.SetFocus();
        foreach (var character in name)
        {
            field.NewKeyDownEvent(new Key(character));
        }
        dialog.Buttons[0].InvokeCommand(Command.Accept);
    }
}
