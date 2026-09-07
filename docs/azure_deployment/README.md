# Azure Deployment Documentation

Use these documents according to the task:

- [Validation and shutdown runbook](validation_and_shutdown_runbook.md) — verify the running dev environment, inspect health and logs, stop/start the services, disable deployment workflows, or delete the complete resource group.
- [OIDC prerequisite setup](oidc_prerequisite_setup.md) — create and configure the Entra application, federated credentials, GitHub environments, secrets, and Azure RBAC permissions.
- [OIDC workflow diagrams](oidc_workflow_diagrams.md) — visualize authentication, infrastructure preview/apply, application deployment, and runtime authorization boundaries.
- [Azure deployment plan](../implementation/azure_deployment_plan.md) — implementation phases, live validation evidence, and remaining network-limited checks.

## Recommended order

1. Use the [validation and shutdown runbook](validation_and_shutdown_runbook.md) when the environment already exists.
2. Use the [OIDC prerequisite setup](oidc_prerequisite_setup.md) only when configuring a new repository or environment.
3. Refer to the [Azure deployment plan](../implementation/azure_deployment_plan.md) for architecture and validation history.
