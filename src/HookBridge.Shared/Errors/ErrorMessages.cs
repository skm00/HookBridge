namespace HookBridge.Shared.Errors;

/// <summary>
/// Shared application error message constants.
/// </summary>
public static class ErrorMessages
{
    /// <summary>
    /// Error returned when an API key is invalid.
    /// </summary>
    public const string InvalidApiKey = "The provided API key is invalid.";

    /// <summary>
    /// Error returned when a tenant cannot be found.
    /// </summary>
    public const string TenantNotFound = "The requested tenant was not found.";

    /// <summary>
    /// Error returned when access is unauthorized.
    /// </summary>
    public const string UnauthorizedAccess = "You are not authorized to access this resource.";
}
