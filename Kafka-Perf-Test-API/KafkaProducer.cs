using Confluent.Kafka;
using System.Text;

namespace Kafka_Perf_Test_API
{
    public sealed class KafkaProducer : IKafkaProducer, IDisposable
    {
        private IProducer<string, string> _producer;
        private readonly IKafkaFactory _kafkaFactory;        
        private readonly ILogger<IKafkaProducer> _logger;

        public KafkaProducer(IKafkaFactory kafkaFactory,            
            ILogger<IKafkaProducer> logger)
        {
            _kafkaFactory = kafkaFactory;            
            _logger = logger;
            _producer = null;
            InitProducer();
        }

        public async Task<DeliveryResult<string, string>> ProduceAsync(string topicName, Message<string, string> message)
        {
            // 3 attempts.. can be converted to use Polly
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    var deliveryResult = await _producer.ProduceAsync(topicName, message);
                    _logger.LogInformation("Delivered message with Partition {@DestinationPartition}, Offset {@DestinationOffset} to topic {@DestinationTopic}",
                        deliveryResult.Partition.Value, deliveryResult.Offset.Value, deliveryResult.Topic);

                    return deliveryResult;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Produce operation to {topicName} topic threw an exception.");
                }
            }

            _logger.LogError($"Failed to Produce message with key {message.Key} to {topicName} topic.");
            return null;
        }
        
        private void InitProducer()
        {
            if (_producer != null) return;
            var producerConfig = _kafkaFactory.GetProducerConfig();
            _producer = _kafkaFactory.GetProducer(producerConfig);
        }

        private void DisposeProducer()
        {
            if (_producer == null) return;
            _logger.LogInformation("Disposing producer.");
            _producer.Flush();
            _producer.Dispose();
            _producer = null;
        }

        public void Dispose()
        {
            DisposeProducer();
        }
    }
}
