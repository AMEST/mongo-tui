namespace MongoTui.Application.Models;

/// <summary>Outcome of an attempt to unlock secrets without user interaction.</summary>
public enum AutoUnlockResult
{
    Unlocked,
    PasswordRequired,
    KeyMaterialUnavailable,
}
