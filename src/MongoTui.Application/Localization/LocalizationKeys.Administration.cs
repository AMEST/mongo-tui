namespace MongoTui.Application.Localization;

public static partial class LocalizationKeys
{
    public static class ChangePassword
    {
        public const string Change = "ChangePassword.Change";
        public const string Changed = "ChangePassword.Changed";
        public const string Field = "ChangePassword.Field";
        public const string Title = "ChangePassword.Title";
    }

    public static class Confirm
    {
        public const string ClearCollectionMessage = "Confirm.ClearCollection.Message";
        public const string ClearCollectionTitle = "Confirm.ClearCollection.Title";
        public const string DropCollectionMessage = "Confirm.DropCollection.Message";
        public const string DropCollectionTitle = "Confirm.DropCollection.Title";
        public const string DropDatabaseMessage = "Confirm.DropDatabase.Message";
        public const string DropDatabaseTitle = "Confirm.DropDatabase.Title";
    }

    public static class Error
    {
        public const string Connection = "Error.Connection";
        public const string InvalidUri = "Error.InvalidUri";
        public const string Load = "Error.Load";
        public const string Locked = "Error.Locked";
        public const string NameRequired = "Error.NameRequired";
        public const string NotFound = "Error.NotFound";
        public const string Operation = "Error.Operation";
        public const string Query = "Error.Query";
        public const string Save = "Error.Save";
        public const string ScriptSize = "Error.ScriptSize";
        public const string TabLimit = "Error.TabLimit";
        public const string Transfer = "Error.Transfer";
        public const string Url = "Error.Url";
    }

    public static class Export
    {
        public const string FieldFilter = "Export.Field.Filter";
        public const string FieldLimit = "Export.Field.Limit";
        public const string FieldOutput = "Export.Field.Output";
        public const string Title = "Export.Title";
    }

    public static class Import
    {
        public const string FieldDrop = "Import.Field.Drop";
        public const string FieldInput = "Import.Field.Input";
        public const string Title = "Import.Title";
    }

    public static class Index
    {
        public const string Create = "Index.Create";
        public const string DeleteHint = "Index.DeleteHint";
        public const string Keys = "Index.Keys";
        public const string Unique = "Index.Unique";
        public const string UniqueBadge = "Index.UniqueBadge";
    }

    public static class Indexes
    {
        public const string Create = "Indexes.Create";
        public const string Drop = "Indexes.Drop";
        public const string FieldBackground = "Indexes.Field.Background";
        public const string FieldKeys = "Indexes.Field.Keys";
        public const string FieldName = "Indexes.Field.Name";
        public const string FieldUnique = "Indexes.Field.Unique";
        public const string Refresh = "Indexes.Refresh";
        public const string Title = "Indexes.Title";
    }

    public static class MasterPassword
    {
        public const string Field = "MasterPassword.Field";
        public const string Title = "MasterPassword.Title";
        public const string Unlock = "MasterPassword.Unlock";
    }

    public static class Security
    {
        public const string Confirm = "Security.Confirm";
        public const string Continue = "Security.Continue";
        public const string Error = "Security.Error";
        public const string ErrorConfirmReset = "Security.Error.ConfirmReset";
        public const string ErrorKekFileMissing = "Security.Error.KekFileMissing";
        public const string ErrorKeyringKeyMissing = "Security.Error.KeyringKeyMissing";
        public const string ErrorKeyringUnavailable = "Security.Error.KeyringUnavailable";
        public const string ErrorNotReady = "Security.Error.NotReady";
        public const string ErrorPasswordMaterialMissing = "Security.Error.PasswordMaterialMissing";
        public const string ErrorTitle = "Security.Error.Title";
        public const string ErrorUnknownKeyMaterial = "Security.Error.UnknownKeyMaterial";
        public const string FileHint = "Security.FileHint";
        public const string Intro = "Security.Intro";
        public const string Invalid = "Security.Invalid";
        public const string Keyring = "Security.Keyring";
        public const string Mismatch = "Security.Mismatch";
        public const string Mode = "Security.Mode";
        public const string ModeErrorPasswordEmpty = "Security.Mode.Error.PasswordEmpty";
        public const string ModeErrorPasswordMismatch = "Security.Mode.Error.PasswordMismatch";
        public const string ModeField = "Security.Mode.Field";
        public const string ModeInitialize = "Security.Mode.Initialize";
        public const string ModeKeyring = "Security.Mode.Keyring";
        public const string ModeKeyringDescription = "Security.Mode.Keyring.Description";
        public const string ModeNone = "Security.Mode.None";
        public const string ModeNoneDescription = "Security.Mode.None.Description";
        public const string ModeNoneWarning = "Security.Mode.None.Warning";
        public const string ModePassword = "Security.Mode.Password";
        public const string ModePasswordDescription = "Security.Mode.Password.Description";
        public const string ModePasswordConfirm = "Security.Mode.PasswordConfirm";
        public const string ModePasswordField = "Security.Mode.PasswordField";
        public const string ModeTitle = "Security.Mode.Title";
        public const string NewPassword = "Security.NewPassword";
        public const string None = "Security.None";
        public const string Password = "Security.Password";
        public const string SettingsApply = "Security.Settings.Apply";
        public const string SettingsChanged = "Security.Settings.Changed";
        public const string SettingsCurrent = "Security.Settings.Current";
        public const string SettingsNew = "Security.Settings.New";
        public const string SettingsPassword = "Security.Settings.Password";
        public const string SettingsPasswordConfirm = "Security.Settings.PasswordConfirm";
        public const string SettingsPasswordHint = "Security.Settings.PasswordHint";
        public const string SettingsReset = "Security.Settings.Reset";
        public const string SettingsResetDone = "Security.Settings.ResetDone";
        public const string SettingsResetQuestion = "Security.Settings.ResetQuestion";
        public const string SettingsResetTitle = "Security.Settings.ResetTitle";
        public const string SettingsSwitchConfirm = "Security.Settings.SwitchConfirm";
        public const string SettingsSwitchTitle = "Security.Settings.SwitchTitle";
        public const string SettingsTitle = "Security.Settings.Title";
        public const string Setup = "Security.Setup";
        public const string StatusLocked = "Security.Status.Locked";
        public const string Title = "Security.Title";
        public const string Unlock = "Security.Unlock";
    }

    public static class Stats
    {
        public const string CollectionTitle = "Stats.CollectionTitle";
        public const string Collections = "Stats.Collections";
        public const string DataSize = "Stats.DataSize";
        public const string DatabaseTitle = "Stats.DatabaseTitle";
        public const string Documents = "Stats.Documents";
        public const string Indexes = "Stats.Indexes";
        public const string Name = "Stats.Name";
        public const string StorageSize = "Stats.StorageSize";
    }

    public static class Transfer
    {
        public const string Array = "Transfer.Array";
        public const string Done = "Transfer.Done";
        public const string Download = "Transfer.Download";
        public const string Export = "Transfer.Export";
        public const string ExportHint = "Transfer.ExportHint";
        public const string FieldFormat = "Transfer.Field.Format";
        public const string FieldMode = "Transfer.Field.Mode";
        public const string Import = "Transfer.Import";
        public const string ImportHint = "Transfer.ImportHint";
        public const string Imported = "Transfer.Imported";
        public const string Limit = "Transfer.Limit";
        public const string ProxyNotSupported = "Transfer.ProxyNotSupported";
        public const string Run = "Transfer.Run";
        public const string Title = "Transfer.Title";
    }
}
