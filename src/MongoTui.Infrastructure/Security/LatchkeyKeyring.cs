using Latchkey;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Domain.Exceptions;
using LatchkeyApi = Latchkey.Latchkey;
using LatchkeyFactoryApi = Latchkey.LatchkeyFactory;

namespace MongoTui.Infrastructure.Security;

/// <summary>OS keyring adapter backed by Latchkey.</summary>
public sealed class LatchkeyKeyring : IKeyring
{
    internal const string ServiceName = "com.amest.mongotui";
    private const string DisplayName = "MongoDB TUI Viewer";

    private readonly ILatchkey _store;

    internal LatchkeyKeyring(ILatchkey store, bool isAvailable)
    {
        _store = store;
        IsAvailable = isAvailable;
    }

    public bool IsAvailable { get; }

    public static LatchkeyKeyring Create()
    {
        var options = new LatchkeyOptions
        {
            ServiceName = ServiceName,
            DisplayName = DisplayName,
        };

        bool available;
        try
        {
            available = LatchkeyApi.VerifyPersistence(options);
        }
        catch (Exception)
        {
            available = false;
        }

        return new LatchkeyKeyring(LatchkeyFactoryApi.Create(options), available);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        await ExecuteAsync(() => _store.GetBytesAsync(key, cancellationToken));

    public Task SetAsync(string key, byte[] value, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await _store.SetAsync(key, value, cancellationToken);
            return true;
        });

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            await _store.DeleteAsync(key, cancellationToken);
            return true;
        });

    private static async Task<T> ExecuteAsync<T>(Func<ValueTask<T>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DomainException("The OS keyring is not accessible.", exception);
        }
    }
}
