using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Kafka_Perf_Test_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KafkaProducerController : ControllerBase
    {
        private readonly IKafkaProducer _producer;
        private readonly ILogger<KafkaProducerController> _logger;
        private static readonly Counter ProducedMessagesCounter = Metrics
            .CreateCounter("kafkaproducer_messages_produced_total", "Number of messages produced.");
        private static readonly Gauge MessagesProducedPerSecond = Metrics
            .CreateGauge("kafkaproducer_messages_produced_per_second", "Rate of messages produced per second.");
        private DateTime _lastProducedTime = DateTime.UtcNow;
        private int _messageCountSinceLastCheck = 0;        
        private const int Parallelism = 32; // Number of parallel tasks (adjust based on your hardware)

        public KafkaProducerController(IKafkaProducer producer, ILogger<KafkaProducerController> logger)
        {
            _producer = producer;
            _logger = logger;
        }
        [HttpPost("produce-parallel")]
        public async Task<IActionResult> ProduceMessagesParallel(int messageCount)
        {
            var stopwatch = Stopwatch.StartNew();

            // Create action block to process messages in parallel
            var actionBlock = new ActionBlock<Message<string, string>>(async message =>
            {
                try
                {
                    var deliveryResult = await _producer.ProduceAsync("primary", message);
                    ProducedMessagesCounter.Inc();

                    _logger.LogInformation($"Produced message to: {deliveryResult.TopicPartitionOffset}");
                }
                catch (ProduceException<string, string> e)
                {
                    _logger.LogError($"Failed to deliver message: {e.Message} [{e.Error.Code}]");
                }
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Parallelism
            });
            var transactionObject = new TransactionObject
            {                
                Value = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.\r\n\r\nWhy do we use it?\r\nIt is a long established fact that a reader will be distracted by the readable content of a page when looking at its layout. The point of using Lorem Ipsum is that it has a more-or-less normal distribution of letters, as opposed to using 'Content here, content here', making it look like readable English. Many desktop publishing packages and web page editors now use Lorem Ipsum as their default model text, and a search for 'lorem ipsum' will uncover many web sites still in their infancy. Various versions have evolved over the years, sometimes by accident, sometimes on purpose (injected humour and the like).\r\n\r\n\r\nWhere does it come from?\r\nContrary to popular belief, Lorem Ipsum is not simply random text. It has roots in a piece of classical Latin literature from 45 BC, making it over 2000 years old. Richard McClintock, a Latin professor at Hampden-Sydney College in Virginia, looked up one of the more obscure Latin words, consectetur, from a Lorem Ipsum passage, and going through the cites of the word in classical literature, discovered the undoubtable source. Lorem Ipsum comes from sections 1.10.32 and 1.10.33 of \"de Finibus Bonorum et Malorum\" (The Extremes of Good and Evil) by Cicero, written in 45 BC. This book is a treatise on the theory of ethics, very popular during the Renaissance. The first line of Lorem Ipsum, \"Lorem ipsum dolor sit amet..\", comes from a line in section 1.10.32.Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.\r\n\r\nWhy do we use it?\r\nIt is a long established fact that a reader will be distracted by the readable content of a page when looking at its layout. The point of using Lorem Ipsum is that it has a more-or-less normal distribution of letters, as opposed to using 'Content here, content here', making it look like readable English. Many desktop publishing packages and web page editors now use Lorem Ipsum as their default model text, and a search for 'lorem ipsum' will uncover many web sites still in their infancy. Various versions have evolved over the years, sometimes by accident, sometimes on purpose (injected humour and the like).\r\n\r\n\r\nWhere does it come from?\r\nContrary to popular belief, Lorem Ipsum is not simply random text. It has roots in a piece of classical Latin literature from 45 BC, making it over 2000 years old. Richard McClintock, a Latin professor at Hampden-Sydney College in Virginia, looked up one of the more obscure Latin words, consectetur, from a Lorem Ipsum passage, and going through the cites of the word in classical literature, discovered the undoubtable source. Lorem Ipsum comes from sections 1.10.32 and 1.10.33 of \"de Finibus Bonorum et Malorum\" (The Extremes of Good and Evil) by Cicero, written in 45 BC. This book is a treatise on the theory of ethics, very popular during the Renaissance. The first line of Lorem Ipsum, \"Lorem ipsum dolor sit amet..\", comes from a line in section 1.10.32.Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.\r\n\r\nWhy do we use it?\r\nIt is a long established fact that a reader will be distracted by the readable content of a page when looking at its layout. The point of using Lorem Ipsum is that it has a more-or-less normal distribution of letters, as opposed to using 'Content here, content here', making it look like readable English. Many desktop publishing packages and web page editors now use Lorem Ipsum as their default model text, and a search for 'lorem ipsum' will uncover many web sites still in their infancy. Various versions have evolved over the years, sometimes by accident, sometimes on purpose (injected humour and the like).\r\n\r\n\r\nWhere does it come from?\r\nContrary to popular belief, Lorem Ipsum is not simply random text. It has roots in a piece of classical Latin literature from 45 BC, making it over 2000 years old. Richard McClintock, a Latin professor at Hampden-Sydney College in Virginia, looked up one of the more obscure Latin words, consectetur, from a Lorem Ipsum passage, and going through the cites of the word in classical literature, discovered the undoubtable source. Lorem Ipsum comes from sections 1.10.32 and 1.10.33 of \"de Finibus Bonorum et Malorum\" (The Extremes of Good and Evil) by Cicero, written in 45 BC. This book is a treatise on the theory of ethics, very popular during the Renaissance."
            };
            var payload = System.Text.Json.JsonSerializer.Serialize(transactionObject);
            // Create the message object once
            var message = new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = payload
            };

            // Post messages to the action block in parallel
            for (int i = 0; i < messageCount; i++)
            {
                actionBlock.Post(message);
            }

            // Complete the block and wait for all tasks to finish
            actionBlock.Complete();
            await actionBlock.Completion;

            stopwatch.Stop();
            var totalSeconds = stopwatch.Elapsed.TotalSeconds;

            var messagesPerSecond = messageCount / totalSeconds;
            MessagesProducedPerSecond.Set(messagesPerSecond);

            _logger.LogInformation($"Produced {messageCount} messages in {totalSeconds} seconds ({messagesPerSecond} messages/second)");

            return Ok(new { Result = "Messages produced successfully", MessagesPerSecond = messagesPerSecond });
        }
    }
}
