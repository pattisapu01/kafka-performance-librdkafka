namespace Kafka_Perf_Test_API
{
    public interface IKafkaConsumer
    {
        Task Consume(CancellationToken stoppingToken);
    }
}
