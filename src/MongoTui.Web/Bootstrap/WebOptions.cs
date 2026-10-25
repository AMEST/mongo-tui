namespace MongoTui.Web.Bootstrap;

/// <summary>Local web mode deliberately exposes no network bind option.</summary>
public sealed record WebOptions(int Port = 8765, bool OpenBrowser = false, bool Help = false)
{
    public string Url => $"http://127.0.0.1:{Port}";

    public static WebOptions Parse(string[] arguments)
    {
        var options = new WebOptions();
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--open": options = options with { OpenBrowser = true }; break;
                case "--help" or "-h": options = options with { Help = true }; break;
                case "--port" when ++index < arguments.Length
                    && int.TryParse(arguments[index], out var port) && port is > 0 and <= ushort.MaxValue:
                    options = options with { Port = port }; break;
                default: throw new ArgumentException("Usage: mongo-tui web [--port 8765] [--open]");
            }
        }
        return options;
    }
}
