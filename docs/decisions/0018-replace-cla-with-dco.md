# ADR-0018: Replace the CLA with a Developer Certificate of Origin (DCO)

- Status: accepted
- Date: 2026-07-20
- Supersedes: the CLA decision in [ADR-0017](0017-license-agpl-and-cla.md) (the AGPL-3.0 relicense in ADR-0017 stands)

## Context

ADR-0017 relicensed Veya to AGPL-3.0 and required a **Contributor License
Agreement (CLA)**, enforced by the CLA Assistant bot. A CLA aggregates copyright
and patent grants and gives the steward the right to relicense the project as a
whole — its primary practical value is enabling a **commercial** steward to
dual-license (e.g. AGPL for the community, a proprietary license for paying
customers).

Veya is a non-commercial FOSS project. There is no intent to relicense the
project as a whole or offer a proprietary tier, so the CLA imposed friction on
contributors (a signing step, a bot writing to the repository) without a
matching benefit. The CLA machinery was also operationally awkward: the bot
could not write its signature store to the protected `main` branch without a
personal access token (see #109/#110, #112).

## Decision

Replace the CLA with a **Developer Certificate of Origin (DCO)**, Version 1.1.

- Contributors certify provenance per commit with a `Signed-off-by:` trailer
  (`git commit -s`). No copyright assignment; contributors retain full ownership.
- `.github/workflows/dco.yml` is a self-contained CI check (runs on
  `pull_request` with a read-only token) that verifies every non-merge,
  non-bot commit carries a `Signed-off-by` line whose email matches the author.
  No third-party action, no secrets, no repository writes, no signature store.
- `CLA.md` and `.github/workflows/cla.yml` are removed; `DCO.md` carries the
  DCO 1.1 text and sign-off instructions. `CONTRIBUTING.md` and `README.md` are
  updated accordingly.

## Consequences

- **No unilateral relicensing.** Without a CLA, the project can no longer be
  relicensed as a whole without every contributor's agreement. This is an
  accepted trade-off: it matches the project's non-commercial posture and rules
  out a future dual-licensing model unless this decision is revisited.
- **Lower contributor friction.** No signing step and no bot; contributors add
  `-s` to their commits. The DCO is the same mechanism used by the Linux kernel,
  Git, and many other non-commercial projects.
- **Simpler, more secure automation.** The check needs no PAT and no write
  access, removing the operational problem that motivated #112.
- The DCO check is a normal status check; making it a **required** check in
  branch protection is a separate, optional decision.
