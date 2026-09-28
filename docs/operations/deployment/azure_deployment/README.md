# Azure Deployment Documentation

This folder contains the repository's Azure operations guidance: identity setup, infrastructure validation, and live-environment maintenance. Pipeline mechanics live in [../workflow_actions/github/](../workflow_actions/github/), and the architecture and runtime model live in the feature docs.

Use these documents according to the task:

- [Validation and shutdown runbook](validation_and_shutdown_runbook.md) — verify the running dev environment, inspect health and logs, stop/start the services, disable deployment workflows, or delete the complete resource group.
- [Azure CLI command guide](azure_cli_command_guide.md) — command-by-command reference for authentication, resource provisioning, Bicep deployment, failure diagnosis, Container Apps operations, Service Bus recovery, and cleanup.
- [OIDC prerequisite setup](oidc_prerequisite_setup.md) — create and configure the Entra application, federated credentials, GitHub environments, secrets, and Azure RBAC permissions. Purely procedural; reusable for future environments.
- [OIDC workflow diagrams](oidc_workflow_diagrams.md) — visualize authentication, infrastructure preview/apply, application deployment, and runtime authorization boundaries.

Related documentation:

- Azure platform overview: [../features/azure-platform/README.md](../features/azure-platform/README.md) — architecture, naming, identity, and deployment boundaries.
- Pipeline guides: [Deploy to Azure workflow](../workflow_actions/github/deploy_azure_workflow.md) and [Infrastructure workflow](../workflow_actions/github/infra_workflow.md) — workflow triggers, environment gates, and deployment boundaries.

## Recommended order

1. Use the [validation and shutdown runbook](validation_and_shutdown_runbook.md) when the environment already exists.
2. Use the [OIDC prerequisite setup](oidc_prerequisite_setup.md) only when configuring a new repository or environment.
3. Refer to the [workflow guides](../workflow_actions/github/) to understand or modify the pipelines.
4. Use the [Azure platform feature](../features/azure-platform/README.md) to understand the resource model and output contract.
