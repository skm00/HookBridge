namespace HookBridge.Api.Services.AiDashboard;

public sealed class AiDashboardValidationException : Exception
{
    public AiDashboardValidationException(string fieldName, string message)
        : base(message)
    {
        FieldName = fieldName;
    }

    public string FieldName { get; }
}
