# Prompt to paste into Codex on the new computer

Continue the AES Programmer & Troubleshooter project in this workspace.

Before taking any action, read these files:

1. `transfer/START-HERE.md`
2. `transfer/ESSENTIAL-CHAT-HISTORY.md`
3. `README.md`
4. `docs/CABLE.md`
5. `docs/PROTOCOL.md`
6. `docs/BENCH-VALIDATION.md`
7. `docs/IMAGE-SOURCES.md`
8. `web-demo/.openai/hosting.json`

Treat those files as the recovered context from Codex task
`019f963f-b24a-7e90-aa50-dbdc476c4441`.

Current state:

- Desktop app is version 0.2.0.
- Desktop tests last passed 16/16.
- The simulation-only web demo retains its existing Sites project and URL.
- Current web commit is
  `3ea558c2beb8c0c38309f4e6d47041cfe729ed59`.
- The hosted demo has responsive desktop and mobile layouts.

Important requirements:

- Preserve support for AES 7744F and 7788F.
- Preserve the J1 pin 6 +12 V warning and hardware safety gates.
- Never store or log the subscriber cipher or a Google API key.
- Keep the public web version simulation-only.
- Use real AES manufacturer device and antenna images, never generated
  replacements.
- Preserve the readable training documentation.
- Do not create a new Sites project or change the existing public URL.
- Never expose or persist short-lived Sites source repository credentials.

Inspect the actual files and current Git state before proposing or making new
changes. Do not rebuild finished features unless the user requests it.
