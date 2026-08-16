FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]
COPY ["TransactionValidation.sln", "./"]
COPY ["src/TransactionValidation.Api/TransactionValidation.Api.csproj", "src/TransactionValidation.Api/"]
COPY ["src/TransactionValidation.Configuration/TransactionValidation.Configuration.csproj", "src/TransactionValidation.Configuration/"]
COPY ["src/TransactionValidation.Core/TransactionValidation.Core.csproj", "src/TransactionValidation.Core/"]
COPY ["src/TransactionValidation.Integration/TransactionValidation.Integration.csproj", "src/TransactionValidation.Integration/"]
COPY ["src/TransactionValidation.Messaging/TransactionValidation.Messaging.csproj", "src/TransactionValidation.Messaging/"]
COPY ["src/TransactionValidation.Mock/TransactionValidation.Mock.csproj", "src/TransactionValidation.Mock/"]
COPY ["tests/TransactionValidation.Tests/TransactionValidation.Tests.csproj", "tests/TransactionValidation.Tests/"]
COPY ["tools/TestReportGenerator/TestReportGenerator.csproj", "tools/TestReportGenerator/"]

RUN dotnet restore "TransactionValidation.sln"

COPY . .
RUN dotnet publish "src/TransactionValidation.Api/TransactionValidation.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TransactionValidation.Api.dll"]
