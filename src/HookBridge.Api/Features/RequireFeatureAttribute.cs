using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Features;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireFeatureAttribute : TypeFilterAttribute
{
    public RequireFeatureAttribute(string featureName) : base(typeof(RequireFeatureFilter))
    {
        Arguments = [featureName];
    }
}
