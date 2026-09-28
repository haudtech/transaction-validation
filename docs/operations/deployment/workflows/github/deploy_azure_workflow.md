# Deploy to Azure Workflow Guide (`.github/workflows/deploy-azure.yml`)

This workflow updates the existing Azure Container Apps after the infrastructure deployment has already been created. It is intentionally separate from the Bicep workflow and only deploys application artifacts.

## Trigger and deployment boundary

The workflow runs on:

- push to `main` when `src/**` or the workflow file changes
- manual `workflow_dispatch`

It uses `concurrency` to cancel in-progress deploys for the same ref and stays scoped to application delivery rather than infrastructure provisioning.

## Azure identity and resource resolution

The workflow authenticates to Azure through OIDC and resolves the deployed resource names from the stable infra deployment outputs:

- `resourceGroupName`
- `acrName`
- `apiAppName`
- `mockAppName`

This keeps the deploy step aligned with the Bicep-defined infrastructure and avoids hardcoded Azure names in the workflow.

## Deploy sequence

The workflow does the following:

1. `azure/login@v2` using OIDC
2. resolve resource names from the infra deployment outputs
3. `az acr login`
4. build and push the API image to ACR
5. build and push the Mock image to ACR
6. update the API Container App image revision
7. update the Mock Container App image revision
8. wait for active running revisions
9. probe the API `/healthz` endpoint over HTTPS

The deployment is considered healthy only when the Container App revisions are running and the API reaches its health endpoint.

## Environment gate

The job uses `environment: azure-dev`, which allows environment protections to be added without forcing every app-code push to reapply infrastructure.

## Related docs

- [infra_workflow.md](infra_workflow.md)
- [../../azure_deployment/README.md](../../azure_deployment/README.md)
- [../../features/infrastructure-and-delivery/README.md](../../features/infrastructure-and-delivery/README.md)
