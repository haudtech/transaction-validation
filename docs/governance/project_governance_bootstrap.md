# New Project Governance Bootstrap

## Purpose and authority

Use this checklist when establishing governance for a new project before its first implementation change. It sequences the minimum decisions and authorities needed to guide code generation—whether by a person or an AI assistant—without requiring every possible policy document up front.

This is a navigation and readiness checklist, not a replacement for the linked authorities. Each linked document owns its own rules. Choose only the governance that fits the project’s actual requirements, risks, technologies, and delivery model. Do not copy TransactionValidation-specific constraints into another project as universal rules.

## Agenda

- [Sequential bootstrap checklist](#sequential-bootstrap-checklist)
- [Readiness gate](#readiness-gate)
- [Potentially missing project artifacts](#potentially-missing-project-artifacts)
- [After implementation begins](#after-implementation-begins)
- [Related governance](#related-governance)

## Sequential bootstrap checklist

Complete these steps in order. The governance artifacts can be concise; record decisions and link to evidence rather than writing policy for hypothetical cases.

### 1. Establish the project brief

The project owner supplies an approved problem statement before architecture or implementation decisions are treated as settled. Capture:

- intended outcome, users/actors, and acceptance criteria
- scope, explicit non-goals, and dependencies
- external contracts and observable success/rejection/failure outcomes
- security, privacy, data-handling, availability, and performance constraints that affect the design
- unresolved product or operational decisions and who owns their resolution

The brief is project input, not an implementation plan. If a required decision is unresolved, record it and stop before encoding an assumption in code.

### 2. Establish the governance index and authorities

Create a concise governance README that identifies the authoritative owner for each concern. At minimum, decide whether the project needs authorities for:

- architecture and dependency boundaries
- coding and implementation patterns
- configuration, secrets, and environment contracts
- security and privacy requirements
- quality, testing, and release gates
- documentation ownership and evidence
- feature delivery and exception approval
- AI-assisted implementation, if AI tools will generate or modify artifacts

Use the repository [governance index](README.md) and [documentation standards](documentation/documentation_standards.md) as examples, not mandatory text. Avoid duplicate authorities: each concern should have one owner.

### 3. Define documentation structure and evidence policy

Select only the documentation layers the project needs. State which document owns design, implemented feature behavior, operations, policy, and shared reference material. Require implementation claims to point to source, configuration, tests, workflows, or infrastructure evidence.

Use the [documentation templates](documentation/documentation_templates.md) only after choosing the project’s applicable layers and sections. A template is scaffolding, not policy.

### 4. Define architecture boundaries before code

Document the initial system context and responsibilities sufficiently to guide the first implementation slice:

- components/projects and ownership
- dependency direction and allowed references
- external systems and trust/data boundaries
- contracts that cross project or process boundaries
- where configuration, validation, error translation, and side effects belong
- important invariants and relevant success/failure transitions

Use an architecture document/template appropriate to the project. Record only decisions that materially affect boundaries or future choices; use a lightweight decision record when the rationale would otherwise be lost. Do not require a formal ADR framework for a small project without a real decision-management need.

### 5. Set applicable coding, security, and privacy rules

Choose the implementation rules required by the project brief and architecture. Cover the relevant language/framework conventions, input validation, authorization, secret handling, logging/privacy, error contracts, asynchronous behavior, resilience, and data integrity. Separate reusable principles from technology- or project-specific constraints.

Use [coding standards](coding/coding_standards.md) as an example of an applicability-based catalog. Security and privacy risks MUST be addressed in the brief and design; a standalone threat-model document is warranted when risk, regulation, data sensitivity, or system exposure justifies it, not automatically for every project.

### 6. Establish configuration, dependency, and build policy

Choose the project’s SDK/runtime baseline, package/version management, shared build settings, configuration ownership, environment mapping, and secret sources. Define which format, analyzer, build, and dependency checks are blocking. Add the machine-readable configuration files and reference only behavior that is actually enforced.

Use [configuration and environment](configuration/configuration_and_environment.md) and the [repository configuration reference](configuration/repository_configuration_reference.md) as examples. Exact SDKs, packages, tools, and thresholds are project choices.

### 7. Define verification and quality gates

Map each important acceptance criterion and invariant to the least costly test or check that proves it:

- unit tests for owned decisions and transformations
- integration/host tests for composition and boundary behavior
- runtime/E2E checks only when topology, real protocols, or deployment behavior require them
- format, analyzer, build, security, and quality checks that the project actually adopts

Define test isolation/categories and CI or merge gates only when the delivery model requires them. Coverage thresholds are optional policy decisions; do not impose a numeric target without a rationale and measurement model. See [quality policy](quality/quality_policy.md) and [coverage policy](quality/coverage_policy.md).

### 8. Establish AI-assisted implementation instructions

If AI will generate or modify project artifacts, provide an ordered prompt/process that requires it to inspect the project brief and applicable authorities before editing, identify rule applicability, stop on unresolved conflicts, implement only approved scope, verify, and report evidence. Link the prompt to the authorities rather than embedding stale copies of their rules.

Use the [AI-assisted development standard](ai-assisted-development.md) as the reusable pattern. A prompt guides the process but cannot guarantee correctness or replace human review.

### 9. Establish the delivery workflow and readiness review

Connect requirements, governance selection, design, implementation, verification, documentation, review, and exception handling in a feature delivery workflow. Define a definition of done appropriate to the project’s risk and release process.

Use the [feature delivery workflow](feature_delivery_workflow.md) as an example. Before the first implementation, review the readiness gate below with the project owner and responsible technical/security reviewers.

### 10. Plan the first vertical slice

Select a small, end-to-end behavior from the approved brief. Record its applicable governance rules, ownership, contracts, invariants, verification plan, and documentation owner. Begin implementation only when acceptance criteria and material design decisions are sufficiently clear.

## Readiness gate

Before the first implementation change, confirm:

- [ ] The project brief has an owner, testable outcomes, scope, and non-goals.
- [ ] Governance authorities are discoverable, non-duplicative, and selected for actual project risks.
- [ ] Architecture ownership, dependency direction, boundary contracts, and key invariants are clear enough for the first slice.
- [ ] Applicable coding, security, privacy, configuration, and secret-handling expectations are identified.
- [ ] The build/toolchain and blocking quality checks are chosen or explicitly deferred with an owner and due point.
- [ ] Acceptance criteria have a verification plan at appropriate test boundaries.
- [ ] Documentation ownership and evidence expectations are defined.
- [ ] AI-generated changes, if used, must follow the AI implementation standard and receive human review.
- [ ] The first vertical slice and any unresolved decisions are recorded; no unresolved material assumption is being silently treated as policy.

If an item is not applicable, record why. If a blocking decision is unresolved, resolve it or get an explicit owner-approved deferral before proceeding.

## Potentially missing project artifacts

The following are not currently provided as standalone governance templates for every new project. Decide whether each is needed rather than treating its absence as permission to invent requirements:

| Artifact | Current coverage | Recommendation |
|---|---|---|
| Approved project brief / requirements template | The delivery workflow expects a clear requirement, but no dedicated reusable project brief template is defined here. | Create or adopt one when the project needs a repeatable intake/approval format; keep business decisions owned by the project/product owner. |
| Architecture decision record (ADR) template or decision log | Architecture documentation asks for current boundaries and the workflow says to record cross-cutting decisions, but no dedicated ADR format/process is prescribed. | Add a lightweight ADR convention if consequential decisions need durable rationale, status, and supersession history; otherwise record them in the owning architecture document. |
| Security threat-model template | Security/privacy constraints are part of requirements and feature governance, but there is no universal threat-model process in this bootstrap. | Define a risk-based threat-model process when exposure, sensitive data, regulation, or system criticality warrants it; do not block low-risk projects on an unnecessary template. |
| Project-specific AI instructions | A reusable AI standard exists, but each new project still needs its own authority links, tool capabilities, and project conventions. | Add project instruction files only after the authorities and boundaries are settled; make them point to canonical policy and avoid duplicating it. |

These are deliberate decision points. This bootstrap does not declare the artifacts mandatory for every project.

## After implementation begins

Revisit the checklist when requirements, data sensitivity, deployment topology, dependencies, team ownership, or release risk materially change. Add a governance document only when it has a durable owner and a real policy concern to govern. Keep feature behavior in feature documentation and operational procedures in runbooks; link to those authorities rather than copying them into governance.

## Related governance

- [Governance index](README.md)
- [Feature delivery workflow](feature_delivery_workflow.md)
- [AI-assisted development standard](ai-assisted-development.md)
- [Documentation standards](documentation/documentation_standards.md)
- [Documentation templates](documentation/documentation_templates.md)
- [Repository architecture rules](coding/repository_architecture_rules.md)
- [Coding standards](coding/coding_standards.md)
- [Configuration and environment reference](configuration/configuration_and_environment.md)
- [Repository configuration reference](configuration/repository_configuration_reference.md)
- [Quality policy](quality/quality_policy.md)
- [Coverage policy](quality/coverage_policy.md)
