using Confluent.Kafka;

namespace Kafka_Perf_Test_API
{
    public interface IKafkaProducer
    {
        Task<DeliveryResult<string, string>> ProduceAsync(string topicName, Message<string, string> message);
    }
}
