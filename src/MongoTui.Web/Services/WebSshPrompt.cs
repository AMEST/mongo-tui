using MongoTui.Application.Abstractions.Ssh;

namespace MongoTui.Web.Services;

/// <summary>Routes an SSH challenge to the browser circuit which opened the connection.</summary>
public sealed class WebSshPrompt : ITwoFactorPrompt
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TaskCompletionSource<string?>? _answer;
    public string? Prompt { get; private set; }
    public event Action? Changed;

    public async Task<string?> RequestAsync(string prompt, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Prompt = prompt;
            Changed?.Invoke();
            return await _answer.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            Prompt = null;
            _answer = null;
            Changed?.Invoke();
            _gate.Release();
        }
    }

    public void Answer(string? answer) => _answer?.TrySetResult(answer);
}
