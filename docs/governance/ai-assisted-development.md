# AI-Assisted Development Standard

## Purpose and authority

Use this standard whenever an AI assistant is asked to generate or modify repository code, tests, configuration, infrastructure, or implementation documentation. It operationalizes the [feature delivery workflow](feature_delivery_workflow.md); it does not replace governance authorities, human review, or required verification.

A prompt cannot guarantee that an AI follows policy. This standard makes the required sequence explicit and requires evidence: the assistant must inspect applicable authorities, report applicability, implement within scope, run available checks, and disclose anything it could not verify. Humans remain responsible for reviewing the diff and accepting the result.

### Agenda

- [Required execution sequence](#required-execution-sequence)
- [Reusable implementation prompt](#reusable-implementation-prompt)
- [Review and limits](#review-and-limits)
- [Related governance](#related-governance)

## Required execution sequence

The AI assistant MUST follow these stages in order. It MUST pause and ask for clarification when a requirement, contract, or applicable rule is materially ambiguous; it MUST NOT silently invent a product decision.

1. **Understand the request** - restate the requested outcome, observable behavior, constraints, scope, and non-goals. Identify unresolved decisions before changing files.
2. **Check project readiness** - if the workspace is a new project without established governance, follow the [New Project Governance Bootstrap](project_governance_bootstrap.md) and do not implement until its readiness gate is met or an owner approves a documented deferral. For an established project, continue with its current authorities.
3. **Inspect current state** - read the affected source, tests, project configuration, and nearby implementation patterns. Treat source, configuration, tests, workflows, and infrastructure as evidence of current behavior; do not assume old documentation or generated output is authoritative.
4. **Select and apply governance** - consult the [governance index](README.md) and review the authorities that apply. At minimum, evaluate repository architecture rules and coding standards for implementation changes; add configuration, security, messaging, reliability, observability, quality, documentation, and delivery authorities as the change warrants. Record each relevant stable rule ID/title and where it applies. Mark reviewed but inapplicable rules **Not applicable** with a short reason. Do not apply irrelevant rules mechanically.
5. **Design before editing** - identify owners, dependency direction, public/inter-project contracts, data and security boundaries, configuration changes, and success/failure invariants. Prefer the smallest design consistent with the request and existing architecture. Resolve conflicts between a request and a mandatory rule before implementation; explain the conflict and ask for direction rather than silently waiving the rule.
6. **Implement the approved scope** - make the smallest cohesive changes. Follow applicable MUST/MUST NOT requirements and approved patterns. Do not introduce unrelated cleanup, dependencies, abstractions, generated files, secrets, or speculative behavior. Keep tests focused on observable behavior and important failure/invariant cases.
7. **Verify** - run the relevant targeted tests and repository checks (format, analyzers, build, and broader tests when appropriate). Inspect the final diff. If a check cannot be run, report why; never claim it passed. Fix failures caused by the change or report remaining blockers clearly.
8. **Report evidence** - summarize changed behavior and files, list applied rule IDs and any N/A decisions, report exact verification commands/results, disclose limitations, and identify any human decision still required.

## Reusable implementation prompt

Copy this prompt into the AI coding request and fill in the bracketed fields. Keep the repository rule sources available in the workspace; do not paste stale copies of policy text.

```text
Implement the following repository change:

Request:
[Describe the desired behavior and acceptance criteria.]

Scope and constraints:
[Name the allowed areas/files, compatibility requirements, non-goals, and any known limits.]

Follow docs/governance/ai-assisted-development.md and docs/governance/feature_delivery_workflow.md in order. If this is a new project without established governance, follow docs/governance/project_governance_bootstrap.md first.

Before editing:
1. Restate the request, observable acceptance criteria, scope, and non-goals.
2. Inspect the relevant current source, tests, configuration, and existing patterns.
3. Review docs/governance/README.md and every applicable governing document. Identify each applicable Rule ID/title and explain where it applies. Mark reviewed but inapplicable rules N/A with a reason. Do not skip an applicable MUST/MUST NOT rule.
4. Describe the proposed ownership, contract, boundaries, failure behavior, and verification plan. If requirements conflict, a policy is ambiguous, or an implementation decision is missing, stop and ask me before editing.

After the design is clear:
5. Make only the scoped changes, following all applicable governance requirements and repository conventions.
6. Add/update tests for observable behavior, success/failure paths, and important invariants. Update owning documentation when behavior/contracts changed.
7. Run relevant formatting, analyzer, build, and test checks. Inspect the final diff. Do not claim checks passed unless you ran them; report checks that could not be run and why.

Final response must include:
- summary of behavior and files changed;
- applicable Rule IDs and any N/A rules with reasons;
- verification commands and actual results;
- unresolved risks, limitations, or human decisions.
```

## Review and limits

- AI-generated code is a proposed change, not evidence that the design is correct or secure.
- The assistant MUST NOT claim complete governance compliance based only on its own assertion; list the reviewed authorities and verification evidence.
- Tests do not replace architecture, security, privacy, or code review. Reviewers MUST inspect the diff and validate material assumptions.
- Never provide secrets to an AI prompt. Use placeholders and inspect generated changes for secret or sensitive-data exposure.
- If the assistant cannot access a governing document or run a required check, it MUST disclose that limitation rather than infer compliance.

## Related governance

- [Repository governance index](README.md)
- [Feature delivery workflow](feature_delivery_workflow.md)
- [Coding standards](coding/coding_standards.md)
- [Repository architecture rules](coding/repository_architecture_rules.md)
- [Documentation standards](documentation/documentation_standards.md)
- [Quality policy](quality/quality_policy.md)
