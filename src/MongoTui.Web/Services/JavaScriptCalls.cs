namespace MongoTui.Web.Services;

/// <summary>Stable names exported by the application's browser scripts.</summary>
public static class JavaScriptCalls
{
    public const string SignOut = "mongoTuiAuth.logout";
    public const string ReplaceSession = "mongoTuiAuth.replace";
    public const string SetTheme = "mongoTuiTheme.set";
    public const string ToggleTheme = "mongoTuiTheme.toggle";
    public const string SetLanguage = "mongoTuiTheme.language";
    public const string OpenModal = "mongoTuiModal.open";
    public const string CloseModal = "mongoTuiModal.close";
    public const string OpenMenu = "mongoTuiMenu.open";
    public const string FocusMenuTrigger = "mongoTuiMenu.focus";
    public const string CopyText = "mongoTuiFiles.copy";
    public const string DownloadText = "mongoTuiFiles.downloadText";
    public const string DownloadStream = "mongoTuiFiles.downloadStream";

    public static class Editor
    {
        public const string Import = "import";
        public const string ModulePath = "./_content/MongoTui.Web/editor.js";
        public const string Create = "create";
        public const string GetValue = "getValue";
        public const string GetExecution = "getExecution";
        public const string Destroy = "destroy";
        public const string JavaScript = "javascript";
        public const string Json = "json";
    }
}
