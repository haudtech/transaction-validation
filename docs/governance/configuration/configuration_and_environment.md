# Configuration and environment reference

This document is the repository-level reference for environment contracts, configuration conventions, and runtime assumptions that are shared across projects.

## Configuration ownership

Repository-wide configuration is governed by the standards in [repository_configuration_reference.md](repository_configuration_reference.md).

Project- and environment-specific configuration should be documented in the owning feature or operations document, not here.

## Typical configuration domains

- .NET SDK version and solution selection
- build and package defaults
- code style and analyzer settings
- coverage collection settings
- environment variable conventions
- local development and Docker settings
- Azure deployment and identity assumptions

## Environment conventions

For a .NET 8 solution, the common standard is:

- use a single .NET SDK version via `global.json`
- centralize package versions via `Directory.Packages.props`
- keep shared MSBuild settings in `Directory.Build.props`
- use environment variables or app settings for deploy-time values
- avoid embedding secrets in code or documentation
- keep environment-specific values in deployment or local runtime config, not in feature docs

## Relationship to other docs

- Architecture semantics: [../../architecture/README.md](../../architecture/README.md)
- Feature behavior: [../../features/README.md](../../features/README.md)
- Operational runbooks: [../../operations/README.md](../../operations/README.md)
- Governance policy: [../README.md](../README.md)

## Summary

This reference layer is intentionally narrow: it explains the shared configuration model without duplicating the behavior-level or runbook-level documentation that lives in the feature and operations sections.
