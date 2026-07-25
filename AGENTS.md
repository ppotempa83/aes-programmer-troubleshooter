# Superior AES Programmer project guidance

Before making changes, read:

1. `transfer/START-HERE.md`
2. `transfer/ESSENTIAL-CHAT-HISTORY.md`
3. `README.md`
4. The relevant documentation under `docs/`

The simulation-only web demo under `web-demo/` is a separate Git repository and
existing ChatGPT Sites project. Read its own `README.md` and `AGENTS.md` before
changing it.

## Project invariants

- The public web demo must remain simulation-only.
- The existing public Sites identity and URL must not change.
- AES J1 pin 6 carries +12 V and must not be connected.
- Subscriber cipher values and Google API keys must never be stored, logged,
  committed, echoed, or included in reports.
- Preserve the real AES manufacturer imagery and its source documentation.
- Keep all training documentation available, including the original readable
  training PDFs.
- Physical hardware validation with known-good 7744F and 7788F units is required
  before field deployment.
- Do not create a replacement Sites project when `web-demo/.openai/hosting.json`
  already contains a project ID.

## Source-control safety

- Preserve source code, tests, scripts, documentation, required assets, readable
  files under `transfer/`, manufacturer imagery, and training materials.
- Never commit `artifacts/`, compiled releases, installers, build output,
  dependencies, caches, temporary files, environment files, credentials, keys,
  subscriber cipher values, or the history-transfer ZIP.
- Treat `web-demo/` as its own repository. Do not flatten it into this repository,
  delete its `.git` directory, or replace its history.
- Do not publish either repository through GitHub Pages.

## Validation

From the desktop-application repository root:

```powershell
dotnet test SuperiorAes.sln -c Release
```

For the separate web demo, run from `web-demo/`:

```powershell
npm run lint
npm test
```

Simulation and automated tests do not replace the physical-radio bench-validation
steps in `docs/BENCH-VALIDATION.md`.
