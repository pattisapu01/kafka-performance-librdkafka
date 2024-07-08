using Confluent.Kafka;

namespace Kafka_Perf_Test_API
{
    public interface IKafkaFactory
    {
        IConsumer<string, string> GetConsumer(ConsumerConfig consumerConfig);
        IProducer<string, string> GetProducer(ProducerConfig producerConfig);
        ProducerConfig GetProducerConfig();
        ConsumerConfig GetConsumerConfig();
        string[] GetConsumerTopics();
    }
}
