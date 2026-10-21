namespace MongoTui.Application.Models;

/// <summary>Reason why the persisted security configuration cannot be used.</summary>
public enum SecurityProblem
{
    KeyringUnavailable,
    KeyringKeyMissing,
    KekFileMissing,
    PasswordMaterialMissing,
    UnknownKeyMaterial,
}
