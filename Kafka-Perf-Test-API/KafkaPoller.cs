namespace Kafka_Perf_Test_API
{
    public class KafkaPoller : BackgroundService
    {
        private readonly IKafkaConsumer _kafkaConsumer;
        private readonly ILogger<KafkaPoller> _logger;
        public KafkaPoller(IKafkaConsumer kafkaConsumer, ILogger<KafkaPoller> logger)
        {
            _kafkaConsumer = kafkaConsumer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting message polling service");
            await Task.Run(async () => {
                try
                {
                    await _kafkaConsumer.Consume(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while consuming messages.");
                }
            }, stoppingToken);
        }
    }
}
