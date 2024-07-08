# Kafka Performance Test with C# and librdkafka

This project benchmarks the performance of Kafka producers using the `librdkafka` library and a C# client. The goal is to evaluate the throughput of producing messages to a Kafka broker under various configurations and settings.

## Table of Contents
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Running the Benchmark](#running-the-benchmark)
- [Results](#results)
- [Future Work](#future-work)
- [Contributing](#contributing)
- [License](#license)

## Getting Started

These instructions will help you set up the project on your local machine.

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started)
- [Project Tye](https://github.com/dotnet/tye) (for orchestrating dependencies) [tye is replaced with [.net aspire] (https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) recently!]
- you can also use docker compose to orchestrate the projects.

### Installing

Clone the repository:

```bash
git clone [https://github.com/pattisapu01/kafka-performance-librdkafka.git](https://github.com/pattisapu01/kafka-performance-librdkafka.git)
cd kafka-performance-librdkafka

Configuration
The project uses tye.yaml to configure and manage dependent services like Kafka, Zookeeper, Prometheus, and Grafana.

tye.yaml
yaml
Copy code
name: kafka-perf-test
extensions:
- name: zipkin
services:
- name: zookeeper
  image: confluentinc/cp-zookeeper:7.5.3
  bindings:
   - protocol: http
     port: 2181
     containerPort: 2181
  env:
    - name: ZOOKEEPER_CLIENT_PORT
      value: 2181
  volumes:
  - name: zk-storage-data
    source: /zookeeper/.data
    target: /var/lib/zookeeper/data
  - name: zk-storage-log
    source: /zookeeper/.datalog
    target: /var/lib/zookeeper/log
- name: kafka
  image: confluentinc/cp-kafka:7.5.3
  bindings:  
   - protocol: http
     name: kafka
     port: 9094
     containerPort: 9094
  env:
    - name: KAFKA_BROKER_ID
      value: 1
    - name: KAFKA_ZOOKEEPER_CONNECT
      value: zookeeper:2181
    - name: KAFKA_LISTENERS
      value: "INTERNAL://:29094,EXTERNAL://:9094"
    - name: KAFKA_ADVERTISED_LISTENERS
      value: "INTERNAL://kafka:29094,EXTERNAL://localhost:9094"
    - name: KAFKA_INTER_BROKER_LISTENER_NAME
      value: "INTERNAL"
    - name: KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR
      value: 1
    - name: KAFKA_LISTENER_SECURITY_PROTOCOL_MAP
      value: "INTERNAL:PLAINTEXT,EXTERNAL:PLAINTEXT"
    - name: KAFKA_NUM_NETWORK_THREADS
      value: 4
    - name: KAFKA_NUM_IO_THREADS
      value: 8
    - name: KAFKA_SOCKET_SEND_BUFFER_BYTES
      value: 512000
    - name: KAFKA_SOCKET_RECEIVE_BUFFER_BYTES
      value: 512000
    - name: KAFKA_SOCKET_REQUEST_MAX_BYTES
      value: 209715200
  volumes:
  - name: kafka-storage
    source: /kafka/.data
    target: /var/lib/kafka/data
- name: akhq
  image: tchiotludo/akhq:latest
  bindings:
    - protocol: http
      port: 8080
      containerPort: 8080
  env:
    - name: AKHQ_CONFIGURATION
      value: |
        akhq:
          connections:
            kafka-cluster:
              properties:
                bootstrap.servers: "kafka:29094"
- name: kafka-perf-test-api
  project: Kafka-Perf-Test-API.csproj
  replicas: 1
  bindings:
   - protocol: http
     port: 8001
     containerPort: 80
- name: prometheus
  image: prom/prometheus:latest
  bindings:
    - port: 9090
      containerPort: 9090
  volumes:
    - name: prometheus-config
      source: ./prometheus/prometheus.yml
      target: /etc/prometheus/prometheus.yml
    - name: prometheus-data
      source: ./prometheus
      target: /etc/prometheus
- name: grafana
  image: grafana/grafana:latest
  bindings:
    - port: 3000
      containerPort: 3000
  env:
    - name: GF_SECURITY_ADMIN_PASSWORD
      value: "admin"
  volumes:
    - name: grafana-data
      source: ./grafana
      target: /var/lib/grafana
    - name: grafana-provisioning-datasources
      source: ./grafana/datasources
      target: /etc/grafana/datasources
appsettings.json
Configure the producer and consumer settings in appsettings.json:

json
Copy code
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      }
    ],
    "Enrich": [ "FromLogContext" ],
    "Properties": {
      "Application": "Kafka-Perf-Test-API"
    }
  },
  "SERVICE_NAME": "kafka-perf-test",
  "AllowedHosts": "*",
  "Kafka": {
    "Topics": "primary",
    "ConsumerSettings": {
      "GroupId": "kafka-perf-test-consumer",
      "EnableAutoCommit": "false",
      "MaxPollRecords": 500
    },
    "ProducerSettings": {
      "LingerMs": "50",
      "BatchSize": "32768",
      "MessageTimeoutMs": "3000",
      "CompressionType": "lz4"
    }
  },
  "KAFKA_BOOTSTRAP_SERVERS": "localhost:9094",
  "Zipkin": {
    "Endpoint": "http://zipkin:9411/api/v2/spans"
  }
}
Running the Benchmark
To start the benchmark, run the following command in your terminal:

bash
Copy code
tye run
This command will start all the services defined in the tye.yaml file, including Kafka, Zookeeper, Prometheus, Grafana, and the Kafka producer API.

Producing Messages
You can produce messages in parallel using the following endpoint:

bash
Copy code
curl -X POST "http://localhost:8001/api/kafkaproducer/produce-parallel?messageCount=1000000"
Results
For an input batch of 1 million messages, we achieved around 37,000 messages per second on a 32 Core CPU (Intel i9-14900, 3.2 GHz) with 128 GB of RAM.

Key Configuration Tweaks
Producer Settings:

LingerMs: Set to 50 ms to allow batching of messages.
BatchSize: Set to 32 KB to balance batch size and number of messages.
CompressionType: Using lz4 for better performance.
Broker Settings:

num.network.threads: Increased to 4.
num.io.threads: Increased to 8.
socket.send.buffer.bytes: Set to 512 KB.
socket.receive.buffer.bytes: Set to 512 KB.
socket.request.max.bytes: Set to 200 MB.
Future Work
Benchmarking Kafka consumers to understand the full round-trip performance.
Testing with different payload sizes and compression settings.
Exploring multi-broker Kafka clusters for higher throughput.
Contributing
Contributions are welcome! Please fork the repository and submit pull requests.
