using Confluent.Kafka;
using Serilog;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace Kafka_Perf_Test_API
{
    public class KafkaFactory : IKafkaFactory
    {
        private readonly IConfiguration _configuration;
        public KafkaFactory(IConfiguration configuratioin)
        {
            _configuration = configuratioin;
        }
        public IConsumer<string, string> GetConsumer(ConsumerConfig consumerConfig)
        {
            return new ConsumerBuilder<string, string>(consumerConfig)
                .SetLogHandler((_, l) => Log.Debug(l.Message))
                .Build();
        }

        public ConsumerConfig GetConsumerConfig()
        {
            var consumerConfig = new ConsumerConfig();

            var consumerSettings = _configuration.GetSection("Kafka:ConsumerSettings");
            if (!consumerSettings.Exists())
            {
                Log.Logger.Error("Consumer settings not found in configuration.");
                return consumerConfig;
            }

            try
            {
                consumerSettings.Bind(consumerConfig);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error binding Consumer settings.");
                return consumerConfig;
            }

            consumerConfig.BootstrapServers = _configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? consumerConfig.BootstrapServers ?? "localhost:9092";            

            return consumerConfig;
        }

        public IProducer<string, string> GetProducer(ProducerConfig producerConfig)
        {
            return new ProducerBuilder<string, string>(producerConfig)
                .SetLogHandler((_, l) => Log.Debug(l.Message))
                .Build();
        }

        public ProducerConfig GetProducerConfig()
        {
            var producerConfig = new ProducerConfig();

            var producerSettings = _configuration.GetSection("Kafka:ProducerSettings");
            if (!producerSettings.Exists())
            {
                Log.Logger.Error("Producer settings not found in configuration.");
                return producerConfig;
            }

            try
            {
                producerSettings.Bind(producerConfig);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error binding Producer settings.");
                return producerConfig;
            }

            producerConfig.BootstrapServers = _configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? producerConfig.BootstrapServers ?? "localhost:9092";            
            return producerConfig;
        }

        public string[] GetConsumerTopics()
        {
            var topics = _configuration.GetValue<string>("Kafka:Topics")?.Split(Convert.ToChar(","));
            Log.Logger.Debug("Kafka topics: {@Topics}", topics);
            return topics;
        }
    }
}
