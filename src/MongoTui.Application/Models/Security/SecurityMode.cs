namespace MongoTui.Application.Models;

/// <summary>Determines how the Key Encryption Key (KEK) is stored and unlocked.</summary>
public enum SecurityMode
{
    Password,
    Keyring,
    None,
}
