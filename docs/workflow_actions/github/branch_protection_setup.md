# Branch Protection & Required Checks Setup

## Goal

Enforce a merge gate on `main` so that:

1. All changes to `main` must go through a pull request.
2. CI (build + unit tests + format check) and Integration Tests must pass before merge.
3. The PR branch must be up to date with `main` before merging.
4. No one — including repo admins/owners — can bypass these requirements.
5. Direct pushes, force pushes, and branch deletion on `main` are blocked.

## Final State (Verified 2026-09-09)

A single **Repository Ruleset** named `main` (id `20794465`), `enforcement: active`, targeting `refs/heads/main`, with **no bypass actors**.

### Rules in the ruleset

| Rule | Effect |
|---|---|
| `pull_request` | Merges to `main` only via PR; merge methods: merge, squash, rebase; stale reviews dismissed on push; 0 required approvals |
| `required_status_checks` | Contexts `build` and `integration` (GitHub Actions, integration_id `15368`) must pass; `strict_required_status_checks_policy: true` (branch must be up to date) |
| `non_fast_forward` | Blocks force pushes |
| `deletion` | Blocks deleting `main` |
| `creation` | Blocks creating matching refs outside rules |
| `code_coverage` | Code coverage reporting rule |

The key settings for the goal: `bypass_actors: []` (empty) + `required_status_checks` with both check contexts + `strict_required_status_checks_policy: true`.

### Required check contexts

The required checks map to workflow job names:

- `build` → job `build` in [ci.yml](../../../.github/workflows/ci.yml) (workflow name `CI`, shown in PR UI as `CI / build`)
- `integration` → job `integration` in [integration.yml](../../../.github/workflows/integration.yml) (workflow name `Integration Tests`, shown as `Integration Tests / integration`)

Important: the ruleset stores the **job name** as the check context (`build`, `integration`), not the display string (`CI / build (pull_request)`). Getting this wrong causes required checks to stay in "Expected — Waiting for status to be reported" forever even though the same-named checks succeed.

### Who can merge

Anyone with `push` permission on the repo (currently only `haudtech`, admin), **only after** `build` and `integration` checks pass on an up-to-date PR. The empty bypass list means admins cannot skip this — there is no merge path that avoids the checks.

## Workflow Triggers (context)

[ci.yml](../../../.github/workflows/ci.yml) runs on `push` to `main` and on `pull_request` to `main`. [integration.yml](../../../.github/workflows/integration.yml) runs on `push` to `main`, `pull_request` to `main`, and `workflow_dispatch` (it uses `environment: integration`). Both workflows define a `concurrency` group keyed on workflow + PR number (or ref) with `cancel-in-progress: true`, so pushing a follow-up commit cancels the obsolete in-progress run.

Deliberately, CI does not run on feature-branch pushes: with a PR open, the push run would duplicate the `pull_request` run (same commit, same steps), doubling runner minutes and cluttering the PR check list. Pre-PR feedback is covered by the local `check` task (format:verify → build → test:unit).

Resulting behavior:

| Scenario | CI | Integration |
|---|---|---|
| Feature branch push, no PR | not run (use local `check` task) | not run |
| PR opened / updated | one run | one run |
| Merge to `main` | post-merge validation | post-merge validation |

If pre-PR CI feedback on a branch is ever needed, add `workflow_dispatch` to ci.yml or open a draft PR.

## Pitfalls Hit During Setup

1. **Required check context mismatch.** The ruleset was initially configured with display strings (`CI / build (pull_request)`) instead of job names (`build`). Symptom: checks succeed but the required entries stay "Expected — Waiting for status to be reported" and merge stays blocked. Fix: use the exact check-run name (the job name), which is what GitHub Actions reports as the context.

2. **`update` rule blocking all merges.** The ruleset previously included the `update` rule ("Restrict updates: only allow users with bypass permission to update matching refs"). With an empty bypass list, this blocked everyone — including admins — from updating `main` at all, producing "Merging is blocked: Cannot update this protected ref" even with all checks green. In rulesets (unlike classic branch protection), admins do NOT bypass automatically. Fix: remove the `update` rule; the PR-only requirement is already enforced by the `pull_request` rule.

3. **Stale rule evaluation.** After fixing the ruleset, the PR may still show "Merging is blocked" until the page is refreshed or a new commit/check run triggers re-evaluation.

## CLI Commands Used (full diagnostic & fix process)

All commands use the GitHub CLI (`gh`) authenticated as a repo admin.

### 1. Inventory: rulesets and legacy branch protection

```bash
# List all repository rulesets (id, name, enforcement, target)
gh api repos/haudtech/transaction-validation/rulesets \
  --jq '.[] | {id, name, enforcement, target, source_type: .source_type}'

# Check for legacy branch protection on main (expected 404 after migration to rulesets)
gh api repos/haudtech/transaction-validation/branches/main/protection

# Check for org-level rulesets (this repo is user-owned; 404 is expected)
gh api orgs/haudtech/rulesets
```

### 2. Inspect the ruleset definition

```bash
# Full ruleset: rules, bypass actors, target conditions
gh api repos/haudtech/transaction-validation/rulesets/20794465 \
  --jq '{name, enforcement, target, conditions,
         bypass_actors: [.bypass_actors[] | {actor_id, actor_type, bypass_mode}],
         rules: [.rules[] | {type, parameters}]}'
```

### 3. Diagnose a blocked merge via rule evaluations

```bash
# Recent rule evaluations ("insight" for pushes/merges against protected refs)
gh api repos/haudtech/transaction-validation/rulesets/rule-suites \
  --jq '.rule_suites[0:5][] | {id, result, ref: .ref, pushed_at}'

# Detail of a specific evaluation (this is where "update: fail" was found)
gh api repos/haudtech/transaction-validation/rulesets/rule-suites/3986955300 \
  --jq '{result, actor: .actor_name,
         rule_evaluations: [.rule_evaluations[] | {rule_type, result, enforcement, details}]}'
```

### 4. Verify actual check-run names on the PR head commit

```bash
# The check-run "name" values are the contexts the ruleset must reference
gh api repos/haudtech/transaction-validation/commits/<head-sha>/check-runs \
  --jq '.check_runs[] | {name, status, conclusion, app: .app.slug}'
```

### 5. Fix: update the ruleset (remove `update` rule, set correct check contexts)

```bash
gh api repos/haudtech/transaction-validation/rulesets/20794465 -X PUT --input - <<'JSON'
{
  "name": "main",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/heads/main"],
      "exclude": []
    }
  },
  "bypass_actors": [],
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "allowed_merge_methods": ["merge", "squash", "rebase"],
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": false,
        "require_extra_approval_for_unattributed_changes": true,
        "require_last_push_approval": false,
        "required_approving_review_count": 0,
        "required_review_thread_resolution": false,
        "required_reviewers": []
      }
    },
    { "type": "creation" },
    { "type": "code_coverage" },
    {
      "type": "required_status_checks",
      "parameters": {
        "do_not_enforce_on_create": false,
        "required_status_checks": [
          { "context": "build", "integration_id": 15368 },
          { "context": "integration", "integration_id": 15368 }
        ],
        "strict_required_status_checks_policy": true
      }
    }
  ]
}
JSON
```

Notes:
- `integration_id: 15368` is GitHub Actions; required when specifying `required_status_checks` via API.
- A PUT replaces the ruleset's rules list — include every rule you want to keep.

### 6. Post-fix verification

```bash
# Confirm the stored ruleset matches the intended state
gh api repos/haudtech/transaction-validation/rulesets/20794465 \
  --jq '{name, enforcement, bypass_actors, rules: [.rules[] | {type, parameters}]}'

# Confirm who can merge (need at least push permission)
gh api repos/haudtech/transaction-validation/collaborators \
  --jq '.[] | {login, role: .role_name,
               permissions: (.permissions | to_entries | map(select(.value)) | map(.key))}'
```

Then on the PR: refresh the page, click **Update branch** if `main` moved ahead (strict policy), wait for `build` and `integration` to go green, and merge.

## Maintenance Cheatsheet

| Task | Command / Action |
|---|---|
| Add a new required check | PUT ruleset with the new `{ "context": "<job-name>", "integration_id": 15368 }` entry appended to `required_status_checks` |
| Grant merge rights to a user | `gh api -X PUT repos/haudtech/transaction-validation/collaborators/<user> -f permission=push` — checks still enforced (no bypass) |
| Allow someone to skip checks (emergency) | Add a bypass actor to the ruleset (`bypass_mode: "always"`), or temporarily set enforcement to `evaluate` — prefer time-boxed bypass actors over disabling the ruleset |
| Audit why a merge was blocked | Inspect rule evaluations: `gh api repos/.../rulesets/rule-suites` then `.../rule-suites/<id>` |
| View/edit in UI | https://github.com/haudtech/transaction-validation/rules/20794465 |
