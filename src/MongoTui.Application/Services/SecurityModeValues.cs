using MongoTui.Application.Models;
using MongoTui.Domain.Exceptions;

namespace MongoTui.Application.Services;

internal static class SecurityModeValues
{
    internal static bool NeedsPassword(SecurityMode mode) => mode == SecurityMode.Password;

    internal static void ValidatePassword(SecurityMode mode, string? password)
    {
        if (NeedsPassword(mode) && string.IsNullOrEmpty(password))
        {
            throw new DomainException("Master password must not be empty.");
        }
    }

    internal static bool TryParse(string value, out SecurityMode mode)
    {
        switch (value)
        {
            case "password":
                mode = SecurityMode.Password;
                return true;
            case "keyring":
                mode = SecurityMode.Keyring;
                return true;
            case "none":
                mode = SecurityMode.None;
                return true;
            default:
                mode = default;
                return false;
        }
    }

    internal static string ToSettingValue(SecurityMode mode) => mode switch
    {
        SecurityMode.Password => "password",
        SecurityMode.Keyring => "keyring",
        SecurityMode.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
