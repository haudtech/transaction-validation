# Engineering Documentation Standard

## Purpose

This document is the concise, repo-level summary of the engineering documentation model. It defines this repository's layout and ownership boundaries and acts as the quick-reference version of the detailed guidance in [documentation_standards.md](documentation_standards.md). It is not a universal .NET project template; adapt the structure and technology choices to the requirements of each solution.

### Agenda

- [Standard documentation layers](#standard-documentation-layers)
- [Canonical repository structure](#canonical-repository-structure)
- [Core rules](#core-rules)
- [Source of truth](#source-of-truth)
- [Choose documentation and technology by requirements](#choose-documentation-and-technology-by-requirements)
- [Repository entry points](#repository-entry-points)
- [Governance relationship](#governance-relationship)
- [Governance authority map](#governance-authority-map)
- [Related documentation](#related-documentation)

## Standard documentation layers

This repository uses the following five-layer model. Other solutions may adapt it to their needs while preserving clear ownership and avoiding duplicate authority:

1. Architecture
   - system design, runtime boundaries, and cross-cutting semantics
2. Features
   - product behavior, domain logic, and implementation ownership
3. Operations
   - local setup, testing, deployment, diagnostics, and runbooks
4. Governance
   - standards, repository rules, and policy
5. Reference
   - configuration, environment conventions, and shared references

A topic belongs to exactly one layer. When another layer needs it, the document should link to it rather than duplicate it.

## Canonical repository structure

```text
README.md
docs/
  README.md
  architecture/
  features/
  operations/
  governance/
  reference/
```

This structure keeps the system model, feature behavior, operational procedures, policy, and shared configuration references separate and discoverable.

## Core rules

- One source of truth per concern
- One owner per concern
- No duplicate authority across layers
- No historical cleanup notes in active docs
- Feature docs do not redefine architecture
- Operations docs do not redefine feature behavior
- Governance docs are policy, not historical records
- Reference docs hold repo-wide configuration context, not product behavior

## Source of truth

The active documentation set must be grounded in evidence from:

- source code and project composition
- configuration and environment contracts
- infrastructure definitions and deployment manifests
- tests and validation workflows
- runtime behavior that is currently implemented and supported

If a statement is not backed by current repository evidence, it should not remain in the active documentation set.

## Choose documentation and technology by requirements

Use this repository's documentation layers as a starting structure, then create documents only for concerns the solution actually has. Select runtime, persistence, messaging, hosting, and deployment technologies from the business and operational requirements; do not treat .NET, EF Core, or any other library as mandatory merely because it appears in an example.

For example, document persistence boundaries when the solution persists data, messaging behavior when it publishes or consumes messages, and deployment topology when it has deployed environments. Mark genuinely inapplicable concerns out of scope rather than creating empty or speculative documents.

## Repository entry points

This repository maintains these entry points:

- root README
- docs/README.md
- docs/architecture/README.md
- docs/features/README.md
- docs/operations/README.md
- docs/governance/README.md
- docs/reference/README.md

## Governance relationship

This document is the summary-level standard.
The detailed authority for authoring rules, ownership policy, and evidence requirements is [documentation_standards.md](documentation_standards.md).

Use this file as the quick policy reference and use the detailed standards document as the enforcement guide.

## Governance authority map

Each concern has one policy owner. The authoritative document defines the rule; other documents should link to it rather than restating it.

| Concern | Authority | Boundary |
|---|---|---|
| Documentation ownership, evidence, and authoring | [documentation_standards.md](documentation_standards.md) | Governs documentation shape and claims; does not define feature behavior. |
| Reusable coding principles and approved implementation patterns | [../coding/coding_standards.md](../coding/coding_standards.md) | Distinguishes generally reusable guidance from patterns evidenced in this repository. |
| TransactionValidation project boundaries and invariants | [../coding/repository_architecture_rules.md](../coding/repository_architecture_rules.md) | Repository-specific constraints; not a universal .NET architecture prescription. |
| Application configuration conventions | [../configuration/configuration_and_environment.md](../configuration/configuration_and_environment.md) and the owning feature document | Shared conventions live in the reference; feature-specific settings belong to the feature owner. |
| Root build/editor/coverage configuration | [../configuration/repository_configuration_reference.md](../configuration/repository_configuration_reference.md) | Explains machine-readable repository settings; does not replace those files. |
| Quality gates and coverage intent | [../quality/quality_policy.md](../quality/quality_policy.md) and [../quality/coverage_policy.md](../quality/coverage_policy.md) | Defines required assurance; execution procedures belong in operations documentation. |
| Feature-delivery workflow and definition of done | [../feature_delivery_workflow.md](../feature_delivery_workflow.md) | Defines the delivery sequence and review checklist; it does not replace the standards that own individual rules. |
| Implemented feature behavior | The owning document under `docs/features/` | Must be supported by implementation and verification evidence. |
| Runtime facts and implementation evidence | Source code, configuration, tests, workflows, and infrastructure definitions | These artifacts establish what currently runs; documentation explains and links to them. |
| Reusable document scaffolds | [documentation_templates.md](documentation_templates.md) | Templates aid authoring but cannot override the governing standards. |

## Related documentation

- [documentation_standards.md](documentation_standards.md): detailed authoring and governance policy
- [../coding/repository_architecture_rules.md](../coding/repository_architecture_rules.md): repo-specific architectural constraints
- [../configuration/repository_configuration_reference.md](../configuration/repository_configuration_reference.md): repo-level build, formatting, and coverage configuration
- [../README.md](../README.md): documentation index entry point
