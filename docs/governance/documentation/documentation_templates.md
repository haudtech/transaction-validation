# Documentation Templates and Scaffolding Checklist

This document provides authoring scaffolds for the repository documentation set. Templates are aids, not policy authorities: when a template and a governing standard differ, follow the standard and update the template. Select technologies and document sections based on requirements; do not retain irrelevant sections as empty boilerplate.

## 1. Ready-to-use scaffolding checklist

Use this checklist when creating or reshaping a .NET solution. Choose the SDK version, persistence technology, hosting model, and infrastructure from the business, security, and operational requirements; EF Core and other example technologies are optional.

- [ ] Create the root README.md with purpose, platform summary, and technology stack
- [ ] Create docs/README.md as the documentation index
- [ ] Create docs/architecture/README.md and add core architecture documents
- [ ] Create docs/features/README.md and add one feature folder per major domain
- [ ] Create docs/operations/README.md and add local, testing, deployment, and diagnostics docs
- [ ] Create docs/governance/README.md and add standards and repository policy docs
- [ ] Create docs/reference/README.md for shared configuration and environment references
- [ ] Establish coding, architecture, configuration, quality, and documentation authorities before feature implementation
- [ ] Identify which standards are reusable principles and which are solution-specific constraints
- [ ] Define the applicable implementation patterns and how each will be verified
- [ ] Keep the repository root config files in root-level locations, not in docs
- [ ] Ensure each document has a single ownership layer and a clear scope
- [ ] Create a domain-specific feature guide for each implemented feature area using the required feature template
- [ ] Remove historical or implementation-era docs from the active documentation tree
- [ ] Verify that all links point to the current canonical location
- [ ] Ensure no doc duplicates another layer's content

Governance rules define expected practice; feature guides document implemented behavior and link to applicable rules and verification evidence.

Use the [feature delivery workflow](../feature_delivery_workflow.md) to select applicable governance rules and apply the definition-of-done checklist.

## 2. Standard markdown template for the root README

```md
# Project Name

Short description of the product and purpose.

## Platform purpose

Explain the business problem and system responsibility.

## Request flow

1. Receive request
2. Validate input
3. Enforce security rules
4. Process business logic
5. Persist or publish
6. Return response or accept async processing

## Technology stack

- .NET [selected SDK/target framework]
- [Web or application framework, if used]
- [Persistence technology, if the solution persists data]
- [Messaging, cache, cloud, or other infrastructure technologies, if used]
- [Test and verification technologies selected for the solution]

## Operating modes

Describe how the system runs in local and deployed environments.

## Documentation

- docs/README.md
- docs/architecture/README.md
- docs/features/README.md
- docs/operations/README.md
- docs/governance/README.md
```

## 3. Standard markdown template for docs/README.md

```md
# Documentation Tree

This is the main navigation entry point for the project documentation.

## 1. Architecture

- Architecture overview
- API flow and contracts
- Data and persistence

## 2. Features

- Feature area
- API behavior
- Observability

## 3. Operations

- Operations
- Local development
- Testing

## 4. Governance

- Governance
- Documentation standards
- Repository architecture rules

## 5. Reference

- Reference
- Configuration and environment
```

## 4. Standard markdown template for architecture docs

```md
# Architecture Overview

## Purpose

Describe the shared system model and why it exists.

## Scope

Define what is in scope and what is intentionally out of scope.

## System boundaries

- project responsibilities
- runtime dependencies
- external systems
- security boundaries

## Runtime flow

Describe the primary request lifecycle.

## Data and state model (when applicable)

Describe persistence, state ownership, consistency, and data boundaries when the solution stores or manages state. Name a persistence technology only when selected by the requirements.

## Integration model

Describe the integrations the solution actually uses, such as HTTP APIs, messaging, or other downstream dependencies. Mark inapplicable integration types out of scope.

## Reliability model

Describe applicable timeout, retry, resilience, idempotency, and failure handling. Distinguish request/data semantics from operational recovery where both exist.

## Ownership

Document which project or component owns each major responsibility.

## Related docs

- Feature docs
- Operations docs
- Governance docs
```

## 5. Standard markdown template for feature README files

```md
# Feature Name

## Purpose and scope

Describe the feature's business purpose, included behavior, and explicit non-goals.

## Behavior inventory

- List implemented behaviors and important outcomes.
- Include relevant success, rejection, duplicate, and failure cases.

## Technologies and techniques

| Behavior | Technology or technique | Implementation owner | Verification |
|---|---|---|---|
| [Implemented behavior] | [Only technologies actually used] | [Project/service] | [Test or check] |

## Definitions and semantics

Define domain terms, contracts, invariants, state transitions, and boundary semantics needed to understand the feature.

## Implementation pattern rules

For each applicable mandatory rule, record its stable rule ID and title, applicability to this feature, how the implementation follows its approved pattern, and the verification evidence. Link to the governing standard rather than copying its policy. If an exception is approved, link or refer to its exception record and rationale.

| Rule ID and title | Applicability and implementation | Approved pattern | Verification/evidence | Exception record (if any) |
|---|---|---|---|---|
| [Stable ID, title, and governance link] | [Why it applies and how implementation follows it] | [Canonical pattern or implementation shape] | [Test, analyzer, build/review check, or source evidence] | [Approved exception link or N/A] |

## Configuration contract

Document the feature-owned settings, required/optional status, safe defaults, validation, and environment-variable mapping. Never include secret values. Link to shared configuration references instead of duplicating them.

## Source ownership

| Project or component | Responsibility |
|---|---|
| [Project/component] | [Owned behavior or boundary] |

## Verification surface

List the tests, analyzers, build/quality checks, and runtime checks that prove behavior and applicable invariants. Link to operational instructions for command sequences.

## Operational boundaries

Document important runtime limitations, external dependencies, failure boundaries, and unsupported or out-of-scope behavior. Link to the owning runbook for procedures.

## Related supporting documents

- [Architecture, operations, configuration, and governance documents that own related concerns]
```

## 6. Standard markdown template for operations docs

```md
# Local Development / Deployment / Testing Guide

## Purpose

What this guide covers.

## Prerequisites

- required tooling
- required infrastructure
- required secrets or environment values

## Setup

Document the setup steps clearly and in order.

## Run

Document the commands required to execute the workflow.

## Validate

Document the proof that the environment or workflow is healthy.

## Troubleshooting

List known issues and investigation steps.

## Related docs

- Architecture docs
- Feature docs
- Governance docs
```

## 7. Standard markdown template for governance docs

```md
# Governance / Standards Name

## Purpose

Why this standard exists.

## Scope

What it applies to.

## Rules

1. Rule 1
2. Rule 2
3. Rule 3

## Ownership

Who owns the policy and how it is reviewed.

## Related docs

- Architecture docs
- Feature docs
- Operations docs
```

## 8. Standard markdown template for reference docs

```md
# Configuration and Environment Reference

## Purpose

Explain the shared configuration model and environment assumptions.

## Configuration ownership

Identify which settings are repo-level vs feature-level vs environment-level.

## Standard conventions

- .NET SDK version policy
- package version governance
- local vs deployment settings
- secret handling rules

## Related docs

- Architecture docs
- Feature docs
- Operations docs
- Governance docs
```

## 9. Standard authoring guidelines

- Use one canonical doc per concern
- Use the smallest viable doc that captures the true state of the system
- Prefer links over duplication
- Keep docs evidence-based and current
- Separate system design from operational runbooks
- Keep repository policy in governance or reference
- Keep feature behavior in feature docs
- Remove historical and transitional content from the active tree unless it has explicit archival value

## Related documentation

- [README.md](../README.md)
- [engineering_documentation_standard.md](engineering_documentation_standard.md)
- [documentation_standards.md](documentation_standards.md)
