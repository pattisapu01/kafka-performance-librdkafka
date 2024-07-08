FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Kafka-Perf-Test-API.csproj", "./"]
RUN dotnet restore "Kafka-Perf-Test-API.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Kafka-Perf-Test-API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Kafka-Perf-Test-API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kafka-Perf-Test-API.dll"]
