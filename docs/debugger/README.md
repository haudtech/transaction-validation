# Debugger, IntelliSense, and Development-Phase Stability Case Study

## Case study summary

This repository originally showed a classic development-phase problem: the codebase could compile and the solution could appear healthy, while VS Code still reported broken symbol navigation, stale references, or incomplete IntelliSense. The root issue was not the business logic itself; it was the workspace graph and debug environment used by the C# language service during implementation.

In software development life cycle (SDLC) terms, this is an environment and workflow defect, not a runtime defect. It appears during the development and validation phases, when engineers are iterating rapidly and depend on the editor to be accurate. A stable debug session and reliable IntelliSense are not optional conveniences; they are part of the build quality and developer trust model.

This issue is now fixed by stabilizing the workspace graph and documenting the rule that IDE health must be treated as a project artifact, not a local accident.

## The actual problem in context

### Symptoms observed

1. Cross-project references appeared intermittently unresolved.
2. Go to Definition and Find References were inconsistent inside the same solution.
3. Symbols such as `TransactionEnvelope` or `IPartnerVerifier` could look unreferenced even when the project structure was still intentionally incomplete.
4. The editor behaved as if the code were broken even when the repository still built correctly.

### Root cause

The main problem was the C# extension loading a weak or inconsistent project graph. In practice, this meant:

- VS Code was not pinned to a stable default solution for the workspace.
- The workspace graph changed depending on local editor state rather than repo-defined configuration.
- Some symbols were intentionally introduced before their implementations or usage paths were fully wired in the active phase.

That second point is important: early-phase scaffolding often includes empty interfaces, shared DTOs, or placeholder abstractions that are valid but not yet used. A missing reference can be a legitimate phase state, not a bug in the code.

## Evidence from this repository

The fix was grounded in the repo itself:

- The solution file was intentionally kept as the canonical project graph: [TransactionValidation.sln](../../TransactionValidation.sln)
- The tracked workspace settings now pin the default solution: [.vscode/settings.json](../../.vscode/settings.json)
- The relevant abstractions live in the core contracts and model layer:
  - [src/TransactionValidation.Core/Interfaces/IMessagePublisher.cs](../../src/TransactionValidation.Core/Interfaces/IMessagePublisher.cs)
  - [src/TransactionValidation.Core/Interfaces/IPartnerVerifier.cs](../../src/TransactionValidation.Core/Interfaces/IPartnerVerifier.cs)
  - [src/TransactionValidation.Core/Models/TransactionEnvelope.cs](../../src/TransactionValidation.Core/Models/TransactionEnvelope.cs)
- The broader engineering guidance is recorded in [docs/implementation/shared_engineering_principles.md](../implementation/shared_engineering_principles.md)

The repo-level verification workflow that proves the workspace is healthy is:

```bash
dotnet sln TransactionValidation.sln list
dotnet build
dotnet test -v minimal
```

Then, inside VS Code, developers should:

1. Run Developer: Reload Window.
2. Confirm the C# extension is active and the solution loads cleanly.
3. Re-check symbol navigation, especially on the core interfaces and model types.

## Why this matters in the SDLC

During the development phase, the IDE is the primary control plane for the engineer. If IntelliSense and debug navigation are unreliable, then the following fail silently:

- Fast feedback loops degrade.
- Wrong assumptions get encoded into interface design.
- Additional implementation work gets started from inaccurate symbol ownership.
- Debugging becomes slower because the first failure is not the logic bug but the editor environment.

This is exactly why debugging and IntelliSense quality should be treated as engineering hygiene, not a personal editor preference.

## Core principle rules for debug mode and IntelliSense

### Rule 1: Pin the workspace graph to a stable default solution

MUST:
- Keep the repo-solution file as the source of truth for project loading.
- Commit workspace metadata that pins the default solution when the team standardizes on VS Code.
- Ensure the solution graph contains all relevant projects before relying on IntelliSense.

SHOULD:
- Use a tracked default solution file consistently across machines.
- Treat missing references in the editor as a workspace configuration problem until proven otherwise.

Detective evidence:
- The issue was resolved by pinning the default solution and stabilizing the project graph, rather than changing business logic.
- The repo now defines the canonical project graph in [TransactionValidation.sln](../../TransactionValidation.sln) and records the editor setting in [.vscode/settings.json](../../.vscode/settings.json).

### Rule 2: Treat IntelliSense as a build-quality dependency

MUST:
- Consider code navigation, completion, and symbol resolution part of the development pipeline.
- Validate navigation immediately after adding or moving contracts, models, and interfaces.
- If a symbol is intentionally not yet used, document that phase status instead of treating the editor warning as a defect.

SHOULD:
- Rebuild the project graph after major refactors.
- Verify a symbol’s lifecycle: definition, reference, implementation, and call path.

Detective evidence:
- The pattern in this repo showed that some interfaces and model types were intentionally introduced before wiring into full implementations.
- That means a low-usage or zero-usage symbol is not automatically a bug; the workflow must distinguish abstraction scaffolding from missing implementation.

### Rule 3: Debug configuration must be reproducible across the team

MUST:
- Keep debug launch configuration in a versioned and reviewable form when the team depends on it.
- Prefer minimal repo-specific launch and task setup over local machine-specific customizations.
- Document the expected debug workflow for developers joining the project.

SHOULD:
- Add tracked debug assets when multiple developers share the same debugging workflow.
- Validate debug commands against a clean environment before release or handoff.

Detective evidence:
- The original issue was not a crash or failing service; it was editor-level ambiguity during development.
- That class of defect is usually solved by reducing local variation and giving each studio the same workspace graph and debug configuration.

### Rule 4: Distinguish phase-aware scaffolding from broken code

MUST:
- Accept that some symbols exist before full usage is introduced.
- Evaluate editor symptoms against the active lifecycle phase before escalating as an implementation defect.
- Record whether a symbol is "stubbed for later wiring" or "truly disconnected".

SHOULD:
- Maintain a small lifecycle note in the project documentation when scaffolding intentionally introduces abstraction layers early.
- Encourage developers to verify whether the issue is project graph, implementation, or simply a phase mismatch.

Detective evidence:
- In early phases, interfaces and models legitimately appear with limited references.
- An unresolved symbol in a phase 2 or 3 scaffold is not necessarily evidence of a broken system; it may simply mean the wiring stage has not happened yet.

### Rule 5: Verification must include editor health, not just build health

MUST:
- Treat a successful build as necessary but not sufficient evidence.
- Verify the editor resolves symbols correctly after any structural changes.
- Use reload/reindex steps when the C# language service stops tracking the project graph.

SHOULD:
- Include symbol navigation checks in the team’s definition of done for major refactors.
- Add a small checklist to SDLC milestones for IntelliSense and debug mode sanity checks.

Detective evidence:
- This repository proved that compile success and editor correctness are not equivalent states.
- The actual fix was to stabilize the environment that the C# server uses to resolve references and definitions.

## Investigation pattern used here

The systematic approach that resolved the issue was:

1. Confirm that the build itself was not the failing layer.
2. Check the project graph and default workspace configuration.
3. Evaluate whether missing symbol usage was phase-appropriate or genuinely broken.
4. Align the IDE configuration with the repo’s canonical solution.
5. Verify the result through reload and navigation checks.

This is the same pattern engineers should follow whenever IntelliSense or debugging fails without a clear compile error.

## Related rules and references

For the wider engineering standards in this repository, see:

- [docs/implementation/shared_engineering_principles.md](../implementation/shared_engineering_principles.md)
- [README.md](../../README.md)

## Short takeaway

A development environment that cannot resolve symbols is not a cosmetic issue. It is an SDLC quality issue because it slows design, weakens debugging, and creates false confidence during implementation. In this repository, the fix was not to manufacture references; it was to stabilize the workspace graph and document the principle that debug mode and IntelliSense are part of healthy project delivery.
