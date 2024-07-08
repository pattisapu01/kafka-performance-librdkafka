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
