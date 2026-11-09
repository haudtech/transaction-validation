FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]
COPY ["TransactionValidation.sln", "./"]
COPY ["src/TransactionValidation.Api/TransactionValidation.Api.csproj", "src/TransactionValidation.Api/"]
COPY ["src/TransactionValidation.Configuration/TransactionValidation.Configuration.csproj", "src/TransactionValidation.Configuration/"]
COPY ["src/TransactionValidation.Core/TransactionValidation.Core.csproj", "src/TransactionValidation.Core/"]
COPY ["src/TransactionValidation.Gateway/TransactionValidation.Gateway.csproj", "src/TransactionValidation.Gateway/"]
COPY ["src/TransactionValidation.Gateway.Contracts/TransactionValidation.Gateway.Contracts.csproj", "src/TransactionValidation.Gateway.Contracts/"]
COPY ["src/TransactionValidation.Gateway.Management/TransactionValidation.Gateway.Management.csproj", "src/TransactionValidation.Gateway.Management/"]
COPY ["src/TransactionValidation.Gateway.PolicyStore/TransactionValidation.Gateway.PolicyStore.csproj", "src/TransactionValidation.Gateway.PolicyStore/"]
COPY ["src/TransactionValidation.Integration/TransactionValidation.Integration.csproj", "src/TransactionValidation.Integration/"]
COPY ["src/TransactionValidation.Messaging/TransactionValidation.Messaging.csproj", "src/TransactionValidation.Messaging/"]
COPY ["src/TransactionValidation.Mock/TransactionValidation.Mock.csproj", "src/TransactionValidation.Mock/"]
COPY ["tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj", "tests/TransactionValidation.Tests/"]
COPY ["tools/TestReportGenerator/TestReportGenerator.csproj", "tools/TestReportGenerator/"]

RUN dotnet restore "TransactionValidation.sln"

COPY . .
RUN dotnet publish "src/TransactionValidation.Api/TransactionValidation.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TransactionValidation.Api.dll"]
