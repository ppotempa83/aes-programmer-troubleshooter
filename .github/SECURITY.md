# Security policy

## Supported versions

Security fixes are applied to the latest `main` branch and the latest published
release. Older beta builds may not receive fixes.

## Reporting a vulnerability

Please do not open a public issue for a suspected vulnerability. Use GitHub's
Private vulnerability reporting for this repository when available. If that
option is unavailable, contact the repository owner privately through GitHub
with the affected version, reproduction steps, impact, and any proposed
mitigation. Do not include passwords, subscriber cipher values, API keys, or
other secrets in a report.

## Security boundaries

- Geoapify API keys must remain local and must never be committed or logged.
- Subscriber cipher values and credentials must never be stored in issues,
  reports, screenshots, releases, or test fixtures.
- AES J1 pin 6 carries +12 V and must not be connected to a PC or programmer.
- Simulation and automated tests do not replace physical-radio bench validation.
