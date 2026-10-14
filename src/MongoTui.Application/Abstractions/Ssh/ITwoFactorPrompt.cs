namespace MongoTui.Application.Abstractions.Ssh;

/// <summary>Requests an ephemeral SSH authentication response; null cancels authentication.</summary>
public interface ITwoFactorPrompt
{
    Task<string?> RequestAsync(string prompt, CancellationToken cancellationToken);
}
