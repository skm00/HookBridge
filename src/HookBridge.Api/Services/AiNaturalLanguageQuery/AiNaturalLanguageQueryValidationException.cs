namespace HookBridge.Api.Services.AiNaturalLanguageQuery;

public sealed class AiNaturalLanguageQueryValidationException : Exception
{
    public AiNaturalLanguageQueryValidationException(string fieldName, string message)
        : base(message)
    {
        FieldName = fieldName;
    }

    public string FieldName { get; }
}
