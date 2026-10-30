namespace MongoTui.Application.Localization;

/// <summary>Central names for the terminal and web localization resources.</summary>
public static partial class LocalizationKeys
{
    public static class About
    {
        public const string CoAuthor = "About.CoAuthor";
        public const string Copyright = "About.Copyright";
        public const string Description = "About.Description";
        public const string License = "About.License";
        public const string Title = "About.Title";
        public const string Version = "About.Version";
    }

    public static class App
    {
        public const string Connections = "App.Connections";
        public const string Navigation = "App.Navigation";
        public const string Tabs = "App.Tabs";
        public const string Theme = "App.Theme";
        public const string Title = "App.Title";
    }

    public static class Cli
    {
        public const string Usage = "Cli.Usage";
    }

    public static class Common
    {
        public const string Cancel = "Common.Cancel";
        public const string Close = "Common.Close";
        public const string Continue = "Common.Continue";
        public const string Copy = "Common.Copy";
        public const string CopyFailed = "Common.CopyFailed";
        public const string Create = "Common.Create";
        public const string Delete = "Common.Delete";
        public const string Edit = "Common.Edit";
        public const string Error = "Common.Error";
        public const string Loading = "Common.Loading";
        public const string Next = "Common.Next";
        public const string No = "Common.No";
        public const string Ok = "Common.Ok";
        public const string Optional = "Common.Optional";
        public const string Previous = "Common.Previous";
        public const string Refresh = "Common.Refresh";
        public const string Save = "Common.Save";
        public const string Saved = "Common.Saved";
        public const string Working = "Common.Working";
        public const string Yes = "Common.Yes";
    }

    public static class Menu
    {
        public const string Collection = "Menu.Collection";
        public const string CollectionExport = "Menu.Collection.Export";
        public const string CollectionImport = "Menu.Collection.Import";
        public const string CollectionIndexes = "Menu.Collection.Indexes";
        public const string Connection = "Menu.Connection";
        public const string ConnectionChangePassword = "Menu.Connection.ChangePassword";
        public const string ConnectionDisconnect = "Menu.Connection.Disconnect";
        public const string ConnectionManage = "Menu.Connection.Manage";
        public const string ConnectionRefresh = "Menu.Connection.Refresh";
        public const string ConnectionUnlock = "Menu.Connection.Unlock";
        public const string Document = "Menu.Document";
        public const string DocumentDelete = "Menu.Document.Delete";
        public const string DocumentEdit = "Menu.Document.Edit";
        public const string DocumentInsert = "Menu.Document.Insert";
        public const string File = "Menu.File";
        public const string FileExit = "Menu.File.Exit";
        public const string Help = "Menu.Help";
        public const string HelpAbout = "Menu.Help.About";
        public const string HelpShortcuts = "Menu.Help.Shortcuts";
        public const string Query = "Menu.Query";
        public const string QueryAi = "Menu.Query.Ai";
        public const string QueryAutocomplete = "Menu.Query.Autocomplete";
        public const string QueryClose = "Menu.Query.Close";
        public const string QueryHistory = "Menu.Query.History";
        public const string QueryNew = "Menu.Query.New";
        public const string QueryRun = "Menu.Query.Run";
        public const string QueryTranslate = "Menu.Query.Translate";
        public const string Settings = "Menu.Settings";
        public const string SettingsAi = "Menu.Settings.Ai";
        public const string SettingsDisplay = "Menu.Settings.Display";
        public const string SettingsLanguage = "Menu.Settings.Language";
        public const string SettingsQueries = "Menu.Settings.Queries";
        public const string SettingsSecurity = "Menu.Settings.Security";
    }

    public static class Nav
    {
        public const string Connections = "Nav.connections";
        public const string History = "Nav.history";
        public const string Logs = "Nav.logs";
        public const string Settings = "Nav.settings";
        public const string Workspace = "Nav.workspace";
    }

    public static class Logs
    {
        public const string AutoRefresh = "Logs.AutoRefresh";
        public const string Contents = "Logs.Contents";
        public const string Empty = "Logs.Empty";
        public const string File = "Logs.File";
        public const string Latest = "Logs.Latest";
    }

    public static class Reconnect
    {
        public const string Failed = "Reconnect.Failed";
        public const string Rejected = "Reconnect.Rejected";
        public const string Reload = "Reconnect.Reload";
        public const string Retrying = "Reconnect.Retrying";
        public const string Title = "Reconnect.Title";
    }

    public static class Settings
    {
        public const string Appearance = "Settings.Appearance";
        public const string Auto = "Settings.Auto";
        public const string Change = "Settings.Change";
        public const string Dark = "Settings.Dark";
        public const string Language = "Settings.Language";
        public const string LanguageHint = "Settings.LanguageHint";
        public const string Light = "Settings.Light";
        public const string ThemeHint = "Settings.ThemeHint";
    }

    public static class Shortcuts
    {
        public const string Ai = "Shortcuts.Ai";
        public const string Autocomplete = "Shortcuts.Autocomplete";
        public const string ChangePassword = "Shortcuts.ChangePassword";
        public const string CloseTab = "Shortcuts.CloseTab";
        public const string Connections = "Shortcuts.Connections";
        public const string Copy = "Shortcuts.Copy";
        public const string Cut = "Shortcuts.Cut";
        public const string Delete = "Shortcuts.Delete";
        public const string Edit = "Shortcuts.Edit";
        public const string Export = "Shortcuts.Export";
        public const string History = "Shortcuts.History";
        public const string Import = "Shortcuts.Import";
        public const string Indexes = "Shortcuts.Indexes";
        public const string Insert = "Shortcuts.Insert";
        public const string InsertTab = "Shortcuts.InsertTab";
        public const string MacNote = "Shortcuts.MacNote";
        public const string MenuBar = "Shortcuts.MenuBar";
        public const string MoveFocus = "Shortcuts.MoveFocus";
        public const string NewTab = "Shortcuts.NewTab";
        public const string Paste = "Shortcuts.Paste";
        public const string Quit = "Shortcuts.Quit";
        public const string Refresh = "Shortcuts.Refresh";
        public const string Run = "Shortcuts.Run";
        public const string Title = "Shortcuts.Title";
        public const string Translate = "Shortcuts.Translate";
        public const string Unlock = "Shortcuts.Unlock";
    }

    public static class Status
    {
        public const string ActiveConnections = "Status.ActiveConnections";
        public const string CollectionCleared = "Status.CollectionCleared";
        public const string CollectionCreated = "Status.CollectionCreated";
        public const string CollectionDropped = "Status.CollectionDropped";
        public const string Connected = "Status.Connected";
        public const string DatabaseCreated = "Status.DatabaseCreated";
        public const string DatabaseDropped = "Status.DatabaseDropped";
        public const string Disconnected = "Status.Disconnected";
        public const string DisplayMode = "Status.DisplayMode";
        public const string Hint = "Status.Hint";
        public const string Language = "Status.Language";
        public const string QueryResult = "Status.QueryResult";
        public const string Ready = "Status.Ready";
    }
}
