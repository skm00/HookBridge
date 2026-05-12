namespace HookBridge.Worker.KafkaSwapBuffer;

public interface IKafkaSwapBufferConsumerFactory
{
    IKafkaSwapBufferConsumer Create(KafkaConsumerOptions options);
}
