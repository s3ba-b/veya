# Developer Certificate of Origin

Veya uses the **Developer Certificate of Origin (DCO)** instead of a Contributor
License Agreement. The DCO is a lightweight, per-commit certification of
provenance — you keep the copyright to your contribution; you simply attest that
you have the right to submit it under the project's license
([AGPL-3.0-or-later](LICENSE)).

## How to sign off

Add a `Signed-off-by` line to every commit by committing with `-s`:

```sh
git commit -s -m "Your message"
```

This appends a trailer using your configured `user.name` and `user.email`:

```
Signed-off-by: Jane Doe <jane@example.com>
```

To backfill sign-offs on commits you already made:

```sh
git rebase --signoff <base>   # then force-push your branch
```

The email in the `Signed-off-by` line must match the commit author's email. A
CI check (`.github/workflows/dco.yml`) verifies this on every pull request.

## What you are certifying

By signing off, you certify the Developer Certificate of Origin, Version 1.1:

```
Developer Certificate of Origin
Version 1.1

Copyright (C) 2004, 2006 The Linux Foundation and its contributors.

Everyone is permitted to copy and distribute verbatim copies of this
license document, but changing it is not allowed.


Developer's Certificate of Origin 1.1

By making a contribution to this project, I certify that:

(a) The contribution was created in whole or in part by me and I
    have the right to submit it under the open source license
    indicated in the file; or

(b) The contribution is based upon previous work that, to the best
    of my knowledge, is covered under an appropriate open source
    license and I have the right under that license to submit that
    work with modifications, whether created in whole or in part
    by me, under the same open source license (unless I am
    permitted to submit under a different license), as indicated
    in the file; or

(c) The contribution was provided directly to me by some other
    person who certified (a), (b) or (c) and I have not modified
    it.

(d) I understand and agree that this project and the contribution
    are public and that a record of the contribution (including all
    personal information I submit with it, including my sign-off) is
    maintained indefinitely and may be redistributed consistent with
    this project or the open source license(s) involved.
```
