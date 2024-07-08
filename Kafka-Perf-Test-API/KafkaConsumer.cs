using Confluent.Kafka;
using Prometheus;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;

namespace Kafka_Perf_Test_API
{
    public sealed class KafkaConsumer : IKafkaConsumer, IDisposable
    {
        private readonly BufferBlock<Tuple<List<KeyValuePair<string, string>>, ConsumeResult<string, string>>> _messageBuffer;
        private ActionBlock<Tuple<List<KeyValuePair<string, string>>, ConsumeResult<string, string>>> _processorBlock;
        /// <summary>
        /// Thread safe concurent dictionary to store 
        /// </summary>
        ConcurrentDictionary<Tuple<string, int, long>, Tuple<ConsumeResult<string, string>, bool>> messageProcessingResults =
            new ConcurrentDictionary<Tuple<string, int, long>, Tuple<ConsumeResult<string, string>, bool>>();

        // Field to track the last committed offset
        Dictionary<Tuple<string, int>, long> lastCommittedOffsets = new Dictionary<Tuple<string, int>, long>();
        // Initialize a queue to store recent rates for moving average calculation
        Queue<double> recentRates = new Queue<double>();
        int sampleSize = 100;
        private static readonly Counter ProcessedMessagesCounter = Metrics
            .CreateCounter("kafkaconsumer_messages_processed_total", "Number of messages processed.");
        private static readonly Counter ReceivedMessagesCounter = Metrics
            .CreateCounter("kafkaconsumer_messages_received_total", "Number of messages received.");
        private static readonly Histogram KafkaMessageLatency = Metrics
            .CreateHistogram("kafkaconsumer_kafka_message_latency_seconds", "Histogram of message latency in seconds.",
            new HistogramConfiguration
            {
                Buckets = Histogram.LinearBuckets(start: 0.5, width: 0.5, count: 20)
            });
        private static readonly Gauge MessagesReceivedPerSecond = Metrics
            .CreateGauge("kafkaconsumer_messages_received_per_second", "Rate of messages processed per second.");

        private DateTime lastProcessedTime = DateTime.UtcNow;
        private int messageCountSinceLastCheck = 0;
        private IConsumer<string, string> _consumer;
        private readonly IKafkaFactory _kafkaFactory;        
        private readonly ILogger<IKafkaConsumer> _logger;
        private readonly IServiceProvider _serviceProvider;
        

        public KafkaConsumer(IKafkaFactory kafkaFactory,            
            ILogger<IKafkaConsumer> logger,
            IServiceProvider serviceProvider)
        {
            _kafkaFactory = kafkaFactory;            
            _logger = logger;
            _consumer = null;
            _serviceProvider = serviceProvider;

            _messageBuffer = new BufferBlock<Tuple<List<KeyValuePair<string, string>>, ConsumeResult<string, string>>>(new DataflowBlockOptions
            {
                BoundedCapacity = 1000 // Setting a bounded capacity to prevent out-of-memory issues
            });

        }

        public async Task Consume(CancellationToken stoppingToken)
        {
            InitConsumer();
            _processorBlock = new ActionBlock<Tuple<List<KeyValuePair<string, string>>, ConsumeResult<string, string>>>(
                message => ProcessMessageAsync(message),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = stoppingToken
                });

            // Start processing messages (This buffer will be managed by TPL.we just post to this buffer)
            var processingTask = ProcessMessagesFromBuffer(stoppingToken);

            // Define a batch size threshold and a maximum delay for the batch commit
            const int batchSize = 50; // Adjust based on our throughput and latency requirements
            int defaultSeconds = 2; // Default value if the environment variable "messagebus-commit-interval-seconds" is missing or not an integer
            int commitIntervalSeconds;
            bool isValid = int.TryParse(Environment.GetEnvironmentVariable("messagebus-commit-interval-seconds"), out commitIntervalSeconds);
            TimeSpan maxDelay = TimeSpan.FromSeconds(isValid ? commitIntervalSeconds : defaultSeconds);
            List<ConsumeResult<string, string>> batch = new List<ConsumeResult<string, string>>();
            DateTime lastCommitTime = DateTime.UtcNow;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(100)); // Use a short timeout for polling                                        
                    if (consumeResult?.Message != null && !string.IsNullOrEmpty(consumeResult.Message.Value))
                    {
                        #region metrics
                        var now = DateTime.UtcNow;
                        var timeSinceLastMessage = (now - lastProcessedTime).TotalSeconds;
                        lastProcessedTime = now;
                        var inactivityThreshold = 300; // 5 minutes in seconds

                        if (timeSinceLastMessage > inactivityThreshold)
                        {
                            // Reset the queue if the inactivity period is too long
                            recentRates.Clear();
                        }
                        else if (timeSinceLastMessage > 0)
                        {
                            var rate = 1 / timeSinceLastMessage; // Messages per second

                            // Update the queue with the new rate
                            if (recentRates.Count >= sampleSize)
                            {
                                recentRates.Dequeue(); // Remove the oldest rate if we've reached the sample size
                            }
                            recentRates.Enqueue(rate);

                            // Calculate the moving average of rates
                            var movingAverageRate = recentRates.Average();
                            MessagesReceivedPerSecond.Set(movingAverageRate);
                        }
                        if (consumeResult.Message.Timestamp.Type == TimestampType.CreateTime)
                        {
                            var messageAge = (DateTime.UtcNow - consumeResult.Message.Timestamp.UtcDateTime).TotalSeconds;
                            KafkaMessageLatency.Observe(messageAge);
                        }
                        ReceivedMessagesCounter.Inc();
                        messageCountSinceLastCheck++;
                        #endregion
                        var tuple = new Tuple<List<KeyValuePair<string, string>>, ConsumeResult<string, string>>(null, consumeResult);
                        var bufferAdd = _messageBuffer.Post(tuple);
                        _logger.LogInformation($"Event received from {consumeResult.Topic} topic, partition {consumeResult.Partition.Value} & Offset {consumeResult.Offset}");
                        if (!bufferAdd)
                        {
                            // Handle the case where BufferBlock is full
                            await Task.Delay(100); // Simple backoff strategy for now
                        }
                    }

                    // Call CommitAccumulatedSuccessfulBatch if the maximum delay has passed since the last commit                    
                    if (messageProcessingResults.Count > 0 && (messageProcessingResults.Count >= batchSize || (DateTime.UtcNow - lastCommitTime) >= maxDelay))
                    {
                        CommitAccumulatedSuccessfulBatch();
                        lastCommitTime = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Consume operation was canceled.");
                }
                catch (ConsumeException e)
                {
                    _logger.LogError(e, "Consume error: {Reason}", e.Error.Reason);                    
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Consume operation threw an exception.");
                    await Task.Delay(5000, stoppingToken); // Wait before retrying
                }
            }

            // Commit any remaining messages            
            _logger.LogInformation($"Exiting out of kafka consumer. Committing batch with latest offset {batch.Last().Offset.Value}. Count of records in batch: {batch.Count}");
            CommitAccumulatedSuccessfulBatch();
            if (stoppingToken.IsCancellationRequested)
            {
                await StopAsync();
            }
        }
        public async Task StopAsync()
        {
            // Signal no more messages will be posted.
            _processorBlock.Complete();
            // Wait for all processing to finish.
            await _processorBlock.Completion;
            _logger.LogInformation("All messages have been processed and the processor block is completed.");
        }
        #region background buffer
        private async Task ProcessMessagesFromBuffer(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_messageBuffer.TryReceive(out var message))
                {
                    _processorBlock.Post(message);
                }
                else
                {
                    // If no messages are available, delay to reduce tight looping
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        private async Task ProcessMessageAsync(Tuple<List<KeyValuePair<string, string>>, ConsumeResult<string, string>> consumeResult)
        {
            //ADD LOGIC TO FANOUT TO OTHER TOPICS.
            ProcessedMessagesCounter.Inc();
        }
        #endregion

        void CommitAccumulatedSuccessfulBatch()
        {
            foreach (var topicPartitionGroup in messageProcessingResults.GroupBy(kvp => (kvp.Key.Item1, kvp.Key.Item2)))
            {
                var topic = topicPartitionGroup.Key.Item1;
                var partitionId = topicPartitionGroup.Key.Item2;
                var sortedProcessingResults = topicPartitionGroup.OrderBy(kvp => kvp.Key.Item3).ToList();

                if (!sortedProcessingResults.Any())
                    continue;

                long? firstFailedOffset = null;
                long maxOffset = -1;

                foreach (var result in sortedProcessingResults)
                {
                    if (!result.Value.Item2 && firstFailedOffset == null)
                    {
                        firstFailedOffset = result.Key.Item3;
                        break;
                    }
                    maxOffset = result.Key.Item3;
                }

                long offsetToCommit = firstFailedOffset.HasValue ? firstFailedOffset.Value - 1 : maxOffset;
                var topicPartitionKey = new Tuple<string, int>(topic, partitionId);

                if (!lastCommittedOffsets.ContainsKey(topicPartitionKey))
                    lastCommittedOffsets[topicPartitionKey] = -1;

                if (offsetToCommit > lastCommittedOffsets[topicPartitionKey])
                {
                    var offsetToCommitObj = new TopicPartitionOffset(new TopicPartition(topic, partitionId), offsetToCommit + 1);
                    _consumer.Commit(new[] { offsetToCommitObj });
                    lastCommittedOffsets[topicPartitionKey] = offsetToCommit;

                    _logger.LogInformation($"Committed up to message offset {offsetToCommit} for TopicPartition {topicPartitionKey}.");
                }

                var keysToRemove = sortedProcessingResults
                            .Where(kvp => kvp.Key.Item3 <= offsetToCommit)
                            .Select(kvp => kvp.Key)
                            .ToList();

                foreach (var key in keysToRemove)
                {
                    messageProcessingResults.TryRemove(key, out _);
                }
            }
        }




        private void Commit(ConsumeResult<string, string> result)
        {
            _consumer.Commit(result);
        }

        private void InitConsumer()
        {
            if (_consumer != null) return;
            var consumerConfig = _kafkaFactory.GetConsumerConfig();
            _consumer = _kafkaFactory.GetConsumer(consumerConfig);

            var topics = (from topic in _kafkaFactory.GetConsumerTopics() select topic).ToArray();
            _logger.LogInformation("Subscribing to topics {@topics}", topics);

            _consumer.Subscribe(topics);
            _logger.LogInformation($"Starting consumer...");
        }



        private void DisposeConsumer()
        {
            if (_consumer == null) return;
            _logger.LogInformation("Disposing consumer.");
            _consumer.Unsubscribe();
            _consumer.Close();
            _consumer = null;
        }

        public void Dispose()
        {
            DisposeConsumer();
        }
    }
}
