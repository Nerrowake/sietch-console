# Security Policy

## Supported Versions

Sietch Console is in active alpha development. Only the latest release receives security fixes.

| Version | Supported |
|---------|-----------|
| Latest alpha | Yes |
| Older alphas | No — please upgrade |

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Report security issues privately by emailing **michaeljstoffer@gmail.com** with:

- A description of the vulnerability and its potential impact
- Steps to reproduce or proof-of-concept code
- Any suggested mitigations you have identified

You can expect an acknowledgement within 48 hours and a status update within 7 days. If a fix is warranted, a patched release will be issued and you will be credited in the release notes unless you prefer to remain anonymous.

## Scope

Areas of particular interest for this project:

- **Remote Management API** — Bearer token authentication, rate limiting bypass, unauthorized server control
- **DPAPI credential storage** — improper encryption scope or key derivation for stored WMI passwords and S3 secret keys
- **Cloud sync** — credential leakage, insecure token caching (MSAL), or path traversal in zip extraction
- **SteamCMD execution** — command injection via the server install path or profile fields

## Out of Scope

- Vulnerabilities in Dune: Awakening itself or Funcom's infrastructure
- Issues that require physical access to the machine running Sietch Console
- SmartScreen warnings about the unsigned installer (a known, documented limitation)
