namespace MongoTui.Web.Services;

/// <summary>Deadlines shared by interactive web commands.</summary>
public static class WebOperationTimeouts
{
    public const int CommandSeconds = 30;
    public const int LongOperationSeconds = 120;
    public const int ConnectionSeconds = 180;
}
