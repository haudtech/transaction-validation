# GitHub Workflow Actions Docs

This folder contains documentation for the repository GitHub Actions workflows.

These guides are synchronized with the current workflow files and the latest passing CI setup.

## Available guides

- [CI Workflow Guide](ci_workflow.md)
- [Integration Workflow Guide](integration_workflow.md)
- [Deploy to Azure Workflow Guide](deploy_azure_workflow.md)
- [Infrastructure Workflow Guide](infra_workflow.md)
- [Codecov Configuration Guide](codecov_configuration.md)
- [Branch Protection & Required Checks Setup](branch_protection_setup.md)
- [Workflow Case Studies (Fixed Issues)](workflow_case_studies.md)

## Coverage in each guide

Each guide includes:
1. Command-by-command explanation of workflow steps
2. Trigger conditions and which events start the workflow
3. Manual trigger instructions (GitHub UI and CLI)

Azure operational runbooks (one-time OIDC setup, environment validation/shutdown) live in [../../azure_deployment/](../../azure_deployment/); these guides cover only the pipeline side.
