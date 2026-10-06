namespace Wasla.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Reversible string encryption for provider credentials at rest (AES-256-GCM).
/// Values are version-prefixed so key/algorithm rotation is possible later.
/// </summary>
public interface IStringEncryptor
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
