# GitHub Workflow Actions Docs

This folder contains documentation for the repository GitHub Actions workflows.

These guides are synchronized with the current workflow files and the repository's defined delivery policy.

Workflow documentation explains pipeline behavior, triggers, permissions, environment gates, and delivery boundaries. Azure identity setup, environment validation, and day-two operations live in [../../azure_deployment/README.md](../../azure_deployment/README.md); the workflow guides do not duplicate that operational guidance.

## Available guides

- [CI Workflow Guide](ci_workflow.md)
- [Integration Workflow Guide](integration_workflow.md)
- [Deploy to Azure Workflow Guide](deploy_azure_workflow.md)
- [Infrastructure Workflow Guide](infra_workflow.md)
- [Codecov Configuration Guide](codecov_configuration.md)
- [Branch Protection & Required Checks Setup](branch_protection_setup.md)

## Coverage in each guide

Each guide includes:
1. Trigger conditions and which events start the workflow
2. Required permissions and environment boundaries
3. Delivery responsibilities and health checks
4. Manual trigger instructions (GitHub UI and CLI)

Azure operational runbooks (one-time OIDC setup, environment validation/shutdown) live in [../../azure_deployment/](../../azure_deployment/); these guides cover only the pipeline side.
