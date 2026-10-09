# Reference and configuration

This folder contains repository-level reference material that supports the implementation and operation of the solution without belonging to a feature or a runbook.

## Purpose

Reference docs explain the stable contracts that support delivery, configuration, and environment consistency across the repository.

## Included reference material

- [Configuration and environment reference](../governance/configuration/configuration_and_environment.md): environment variables, runtime assumptions, configuration contracts, and repo settings conventions.
- [Repository configuration reference](../governance/configuration/repository_configuration_reference.md): root-level build, SDK, coverage, and workspace configuration standards.
- [Local environment reference](environment/local_environment.md): operating assumptions for the local development topology and runtime composition.
- [Deployment environment reference](environment/deployment_environment.md): expectations for hosted and deployed environment execution.
- [Application settings reference](configuration/appsettings_reference.md): shared application settings conventions and configuration support notes.

## When to use this layer

Use this layer for:

- repo-wide configuration guidance
- environment and deployment assumptions
- standard settings and conventions
- cross-cutting configuration references

Do not use this layer for feature behavior, runtime flow design, or operational procedures. Those belong under [features](../features/README.md), [architecture](../architecture/README.md), and [operations](../operations/README.md).

## Governance-adjacent templates

Reusable authoring templates and scaffolding guidance are intentionally maintained in the governance documentation layer and linked here only as support material when needed.
