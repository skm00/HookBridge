using HookBridge.Application.Interfaces.Security;
using Microsoft.AspNetCore.Identity;

namespace HookBridge.Infrastructure.Services.Auth;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> _passwordHasher = new();
    private static readonly object User = new();

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(User, password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        var result = _passwordHasher.VerifyHashedPassword(User, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
