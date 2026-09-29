# Anvil Agent Instructions

## Build Plan Tracking

`build plan.md` is the source of truth for Anvil's planned functionality and
milestones.

Whenever a feature, capability, or milestone is completed:

- Update `build plan.md` in the same change.
- Mark the completed item as complete.
- Keep the plan wording accurate if the implementation differs from the original goal.
- Do not claim work is complete if the corresponding plan item is not updated.

When starting a new feature, check `build plan.md` first and use its priorities
and milestone order to guide the work.

## Engineering Quality

Always follow established best practices:

- Prefer clear, idiomatic, maintainable C# and .NET APIs.
- Reuse ASP.NET Core functionality instead of rebuilding equivalent infrastructure.
- Apply secure defaults and validate untrusted input.
- Keep abstractions small, explicit, and documented when their behavior is non-obvious.
- Add or update tests for behavior that changes.
- Run the relevant build, tests, and verification checks before claiming work is complete.
- Avoid unnecessary dependencies, boilerplate, duplication, and speculative features.
