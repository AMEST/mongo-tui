using System.Text;
using AwesomeAssertions;
using FakeItEasy;
using MongoTui.Application.Abstractions.Persistence;
using MongoTui.Application.Abstractions.Security;
using MongoTui.Application.Services;

namespace MongoTui.Application.Tests.Services;

public sealed class AiSettingsServiceTests
{
    private readonly ISettingsStore _settings = A.Fake<ISettingsStore>();
    private readonly IKeyProvider _keyProvider = A.Fake<IKeyProvider>();
    private readonly ICryptoService _crypto = A.Fake<ICryptoService>();
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly SecretKeyService _secrets;
    private readonly AiSettingsService _sut;

    public AiSettingsServiceTests()
    {
        _secrets = new SecretKeyService(_keyProvider, _crypto);
        _sut = new AiSettingsService(_settings, _secrets);

        A.CallTo(() => _settings.GetAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string key, CancellationToken _) =>
                Task.FromResult(_values.TryGetValue(key, out var value) ? value : null));
        A.CallTo(() => _settings.SetAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Invokes((string key, string value, CancellationToken _) => _values[key] = value)
            .Returns(Task.CompletedTask);

        var nonce = new byte[] { 1 };
        A.CallTo(() => _crypto.Encrypt(A<byte[]>._, A<byte[]>._, out nonce)).Returns([9, 9]);
        A.CallTo(() => _crypto.Decrypt(A<byte[]>._, A<byte[]>._, A<byte[]>._)).Returns(Encoding.UTF8.GetBytes("key"));
    }

    [Fact]
    public async Task Should_RoundTripApiKey_When_Unlocked()
    {
        await UnlockAsync();

        await _sut.SaveAsync("https://api.test/v1", "gpt-4o-mini", "key", CancellationToken.None);
        var configuration = await _sut.GetAsync(CancellationToken.None);

        configuration.BaseUrl.Should().Be("https://api.test/v1");
        configuration.Model.Should().Be("gpt-4o-mini");
        configuration.ApiKey.Should().Be("key");
        configuration.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Should_NotBeConfigured_When_ApiKeyMissing()
    {
        await UnlockAsync();

        await _sut.SaveAsync("https://api.test/v1", "gpt-4o-mini", null, CancellationToken.None);

        (await _sut.IsConfiguredAsync(CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Should_NotBeConfigured_When_Locked()
    {
        await UnlockAsync();
        await _sut.SaveAsync("https://api.test/v1", "gpt-4o-mini", "key", CancellationToken.None);

        _secrets.Lock();

        (await _sut.IsConfiguredAsync(CancellationToken.None)).Should().BeFalse();
    }

    private async Task UnlockAsync()
    {
        A.CallTo(() => _keyProvider.UnlockWithMasterPasswordAsync("master", A<CancellationToken>._)).Returns(new byte[32]);
        await _secrets.UnlockAsync("master", CancellationToken.None);
    }
}
