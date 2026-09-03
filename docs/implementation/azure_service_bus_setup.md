# Azure Service Bus setup for E2E validation

This document describes how to provision a real Azure Service Bus namespace for the Azure-native messaging path used by the solution.

This is specifically for validating the Azure Service Bus implementation described in [azure_service_bus_multiple_consumer_plan.md](azure_service_bus_multiple_consumer_plan.md).

## 1. Why a real namespace is required

The Azure Service Bus path in this solution depends on Azure Service Bus topic/subscription semantics:

- one topic
- multiple subscriptions
- independent consumer copies
- subscription filtering
- dead-letter and retry behavior

Azure Service Bus is not equivalent to Azurite. Azurite is useful for some storage scenarios, but it does not faithfully implement the Azure Service Bus broker model required by this project.

For this repository, the valid E2E target is a real Azure Service Bus namespace.

## 2. Prerequisites

Before creating the Service Bus namespace, make sure:

- you are signed in to Azure
- you have an active Azure subscription
- you have permission to create resource groups and messaging resources

Verify access:

```bash
az login
az account list -o table
az account set --subscription "<subscription-name-or-id>"
```

## 3. Create a resource group

```bash
az group create \
  --name rg-txv-dev \
  --location eastus
```

## 4. Create the Service Bus namespace

Use the Standard tier, which supports topics and subscriptions:

```bash
az servicebus namespace create \
  --resource-group rg-txv-dev \
  --name sb-txv-dev-001 \
  --location eastus \
  --sku Standard
```

Important:

- the namespace name must be globally unique
- choose a unique name if the example is already taken

## 5. Get the connection string

```bash
az servicebus namespace authorization-rule keys list \
  --resource-group rg-txv-dev \
  --namespace-name sb-txv-dev-001 \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  --output tsv
```

This value is required for:

- `ServiceBusPublisher__ConnectionString`
- `ServiceBusConsumer__ConnectionString`
- `ServiceBusAuditConsumer__ConnectionString`

## 6. Create the topic and subscriptions

Create the topic:

```bash
az servicebus topic create \
  --resource-group rg-txv-dev \
  --namespace-name sb-txv-dev-001 \
  --name partner.transactions
```

Create the primary subscription:

```bash
az servicebus topic subscription create \
  --resource-group rg-txv-dev \
  --namespace-name sb-txv-dev-001 \
  --topic-name partner.transactions \
  --name partner-transactions
```

Create the audit subscription:

```bash
az servicebus topic subscription create \
  --resource-group rg-txv-dev \
  --namespace-name sb-txv-dev-001 \
  --topic-name partner.transactions \
  --name partner-transactions.audit
```

## 7. Add subscription filters

Primary subscription: broader fan-out filter

```bash
az servicebus topic subscription rule create \
  --resource-group rg-txv-dev \
  --namespace-name sb-txv-dev-001 \
  --topic-name partner.transactions \
  --subscription-name partner-transactions \
  --name primary-match \
  --filter-type SqlFilter \
  --filter-sql-expression "eventType IN ('partner.transaction.accepted','partner.transaction.rejected','partner.transaction.pending')"
```

Audit subscription: accepted-only filter

```bash
az servicebus topic subscription rule create \
  --resource-group rg-txv-dev \
  --namespace-name sb-txv-dev-001 \
  --topic-name partner.transactions \
  --subscription-name partner-transactions.audit \
  --name audit-match \
  --filter-type SqlFilter \
  --filter-sql-expression "eventType = 'partner.transaction.accepted'"
```

## 8. Configure the app for Azure mode

Set the runtime broker to Azure Service Bus:

```bash
export Messaging__BrokerType="AzureServiceBus"
```

Set the publisher options:

```bash
export ServiceBusPublisher__ConnectionString="<connection-string>"
export ServiceBusPublisher__TopicName="partner.transactions"
export ServiceBusPublisher__Subject="partner.transaction"
export ServiceBusPublisher__RoutingKey="partner.transaction.accepted"
export ServiceBusPublisher__EventType="partner.transaction.accepted"
```

Set the mock consumer options:

```bash
export ServiceBusConsumer__Enabled="true"
export ServiceBusConsumer__ConnectionString="<connection-string>"
export ServiceBusConsumer__TopicName="partner.transactions"
export ServiceBusConsumer__SubscriptionName="partner-transactions"
export ServiceBusConsumer__AutoComplete="false"
export ServiceBusConsumer__MaxConcurrentCalls="1"

export ServiceBusAuditConsumer__Enabled="true"
export ServiceBusAuditConsumer__ConnectionString="<connection-string>"
export ServiceBusAuditConsumer__TopicName="partner.transactions"
export ServiceBusAuditConsumer__SubscriptionName="partner-transactions.audit"
export ServiceBusAuditConsumer__AutoComplete="false"
export ServiceBusAuditConsumer__MaxConcurrentCalls="1"
```

## 8a. Managed Identity auth (recommended for Azure deployment)

Connection-string (SAS key) auth is intended for local development only. When the app runs in Azure, leave `*_ConnectionString` empty and set the namespace FQDN instead; `ServiceBusClientFactory` then authenticates with `DefaultAzureCredential`.

```bash
export ServiceBusPublisher__ConnectionString=""
export ServiceBusPublisher__Namespace="sb-txv-dev-001.servicebus.windows.net"

export ServiceBusConsumer__ConnectionString=""
export ServiceBusConsumer__Namespace="sb-txv-dev-001.servicebus.windows.net"

export ServiceBusAuditConsumer__ConnectionString=""
export ServiceBusAuditConsumer__Namespace="sb-txv-dev-001.servicebus.windows.net"
```

Grant the app's system-assigned managed identity the minimum roles on the namespace:

```bash
az role assignment create \
  --assignee-object-id <api-app-identity-principal-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Azure Service Bus Data Sender" \
  --scope $(az servicebus namespace show --resource-group rg-txv-dev --name sb-txv-dev-001 --query id -o tsv)

az role assignment create \
  --assignee-object-id <mock-app-identity-principal-id> \
  --assignee-principal-type ServicePrincipal \
  --role "Azure Service Bus Data Receiver" \
  --scope $(az servicebus namespace show --resource-group rg-txv-dev --name sb-txv-dev-001 --query id -o tsv)
```

`DefaultAzureCredential` also works locally via `az login`, so this path can be exercised outside Azure once the developer's account has the same roles.

## 9. E2E verification checklist

When the Azure Service Bus path is enabled, validate the following:

- [x] namespace exists
- [x] topic exists
- [x] both subscriptions exist
- [x] filters are configured as expected
- [x] app starts with `Messaging__BrokerType=AzureServiceBus`
- [x] a single publish reaches both subscriptions
- [x] both subscribers record the same `messageId`
- [x] both subscribers record the same `correlationId`
- [x] audit path only sees accepted events

## 10. Validation results

As of 2026-09-01, all E2E tests pass with real Azure Service Bus:

```
Passed!  - Failed: 0, Passed: 8, Skipped: 0, Total: 8, Duration: 11 s
```

All test categories validated:
- ✅ Root endpoint authorization
- ✅ Transaction happy path (202 Accepted)
- ✅ Idempotency replay behavior
- ✅ Multiple independent consumer queues (fan-out)
- ✅ Selective binding delivers unverified messages only to primary queue
- ✅ Audit consumer redelivers messages after failure before acknowledgement
- ✅ Validation error handling (400 Bad Request)
- ✅ Missing API key enforcement (401 Unauthorized)

**Conclusion:** Azure Service Bus implementation is production-ready for Azure-native deployment.

## 11. Recommended local policy

For this repository, the recommended pattern is:

- local default: RabbitMQ
- Azure validation: real Azure Service Bus namespace
- do not use Azurite for this Service Bus-based E2E scenario

This keeps the default local development workflow stable while still allowing you to validate the Azure-native message path with the proper broker semantics.
