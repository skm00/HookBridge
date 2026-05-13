using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.Services;

public interface IKernelFactory
{
    Kernel CreateKernel();
}
