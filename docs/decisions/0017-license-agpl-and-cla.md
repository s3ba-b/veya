# ADR-0017: Relicense to AGPL-3.0 and require a Contributor License Agreement

- Status: accepted
- Date: 2026-06-18

## Context

The project was released under Apache-2.0 (added in #47). At this point the
project has a single copyright holder (one contributor), so relicensing is
unilateral and unblocked — no third-party contributor rights stand in the way.

Veya is a network-facing daemon and system assistant whose privacy-transparency
is a product pillar. A strong copyleft license that closes the "software as a
service" loophole aligns with that: anyone who runs a modified Veya as a
networked service must offer their users the corresponding source. Apache-2.0
does not impose this.

Separately, the project will start accepting external contributions. Without a
contributor agreement, each contributor retains their copyright and any future
relicensing would require unanimous consent. Establishing an agreement now —
while the contributor set is still a single person — keeps licensing governance
tractable.

## Decision

1. **Relicense from Apache-2.0 to `AGPL-3.0-or-later`.**
   - `LICENSE` holds the full GNU Affero General Public License v3.0 text.
   - `Directory.Build.props` sets `PackageLicenseExpression` to
     `AGPL-3.0-or-later` (the single source of truth all projects inherit).
   - `NOTICE`, `README.md`, `CONTRIBUTING.md`, and the `scripts/license-scan.sh`
     header are updated to match.
   - The dependency license scan is unchanged in mechanism: incoming
     dependencies must still be **permissive** (MIT/BSD/Apache-2.0/…), so they
     add no obligations on top of the project's own license.

2. **Require a Contributor License Agreement (CLA), enforced via a bot.**
   - `CLA.md` is an Individual/Entity CLA granting the maintainer a broad
     copyright and patent license and the right to relicense the project as a
     whole (subject to remaining open source). Contributors retain ownership of
     their contributions.
   - `.github/workflows/cla.yml` runs the **CLA Assistant** bot
     (`contributor-assistant/github-action`): it comments on first-time
     contributors' PRs and blocks merge via a status check until they sign.
     Signatures are stored in `signatures/version1/cla.json` on the dedicated
     unprotected `cla-signatures` branch (not `main`, which is branch-protected
     and therefore not writable by the action's `GITHUB_TOKEN`).

## Consequences

- **Going forward only.** Versions already published under Apache-2.0 remain
  available under Apache-2.0; this change applies to the project from here on.
  Apache-2.0 is one-way compatible into AGPL-3.0, and as sole copyright holder
  the relicense is unrestricted.
- **CLA chosen over DCO** to preserve the maintainer's ability to relicense the
  project as a whole in the future (a DCO only certifies provenance and would
  not grant that right). The trade-off is a slightly higher bar for first-time
  contributors, mitigated by the bot automating the signature.
- **No PAT required.** The signature store lives on the unprotected
  `cla-signatures` branch, so the action's default `GITHUB_TOKEN` (with
  `contents: write`) can commit signatures without a `PERSONAL_ACCESS_TOKEN`
  secret. `cla-signatures` must stay unprotected. The `uses:` ref in the
  workflow should be pinned to a verified release tag or commit SHA.
- Supersedes the licensing decision in #47.
- The `LICENSE` text shipped in this change was assembled offline from a
  faithful local copy and is reflowed; it should be replaced with the canonical
  FSF text (`https://www.gnu.org/licenses/agpl-3.0.txt`) verbatim.
