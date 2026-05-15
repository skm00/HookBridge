using HookBridge.AI.Worker.Services;
using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.IntegrationTests;

internal sealed class FakeKernelFactory : IKernelFactory
{
    public Kernel CreateKernel() => Kernel.CreateBuilder().Build();
}
