using MongoTui.Domain.Diagnostics;

namespace MongoTui.Application.Diagnostics;

/// <summary>Tracks server-wide changes in application order for one resolved target.</summary>
internal sealed class ProfilerGlobalSettings(ProfilerOptions baseline)
{
    private readonly List<(string? Database, ProfilerOptions Options)> _changes = [(null, baseline)];

    public void Observe(ProfilerOptions actual)
    {
        var expected = _changes[^1].Options;
        if (actual.SlowMs != expected.SlowMs || actual.SampleRate != expected.SampleRate)
            Apply(null, actual);
    }

    public void Apply(string? database, ProfilerOptions options)
    {
        if (database is null) _changes.Clear();
        else Remove(database);
        _changes.Add((database, options));
    }

    public ProfilerOptions? RestorationFor(string database) =>
        _changes[^1].Database == database ? _changes[^2].Options : null;

    public void Remove(string database) => _changes.RemoveAll(change => change.Database == database);

    public void Keep(string database)
    {
        var index = _changes.FindIndex(change => change.Database == database);
        if (index < 0) return;
        _changes[index] = (null, _changes[index].Options);
        _changes.RemoveRange(0, index);
    }
}
