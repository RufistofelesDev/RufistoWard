# Security Policy

RufistoWard is an OAuth 2.0 / OpenID Connect identity server. A vulnerability here can
compromise every application that relies on it for authentication, so we take security
reports seriously and ask that you do too.

## Supported versions

RufistoWard is pre-1.0. Until the `1.0` release, only the latest `master` and the most
recent tagged release receive security fixes.

| Version | Supported |
|---------|-----------|
| `master` (latest) | ✅ |
| Most recent tag | ✅ |
| Older tags | ❌ |

This table will be replaced with a per-minor-version support window once `1.0` ships.

## Reporting a vulnerability

**Please do not open a public issue, pull request, or discussion for security problems.**
Public disclosure before a fix is available puts every deployment at risk.

Report privately through one of these channels:

1. **GitHub Private Vulnerability Reporting** (preferred) — open the repository's
   **Security → Report a vulnerability** tab. This creates a private advisory only the
   maintainers can see.
2. **Email** — `security@rufistoward.dev` *(maintainer: confirm or replace this address
   before the public 1.0 launch)*. Encrypt with the maintainer's public key if you have it.

When you report, please include as much of the following as you can:

- Affected endpoint, flow, or component (e.g. `/connect/token`, refresh-token rotation,
  JWKS, the claim pipeline, the Admin UI).
- The protocol or class of issue (e.g. PKCE downgrade, mix-up / IdP confusion, token
  replay, algorithm confusion, `kid` tampering, open redirect, IDOR in the Admin API).
- A minimal reproduction: request/response captures, a failing token, or a script.
- Version / commit SHA and relevant configuration (issuer, signing algorithm, provider).
- Impact assessment and any suggested remediation.

## What to expect

| Stage | Target |
|-------|--------|
| Acknowledgement of your report | within **3 business days** |
| Initial severity assessment | within **7 business days** |
| Fix or mitigation plan | depends on severity; critical issues are prioritized |
| Coordinated public disclosure | after a fix ships, by mutual agreement |

We follow **coordinated disclosure**: we will work with you on a timeline, credit you in
the advisory and release notes (unless you prefer to remain anonymous), and avoid
publishing details until a fix or mitigation is available.

## Scope

In scope: the protocol surface (authorize, token, device, revocation, introspection,
discovery, JWKS, userinfo, end-session), token construction and key management, the
claim pipeline, the Admin REST API, the Admin/login UI, and the IdentityServer4
migration tooling.

Out of scope: vulnerabilities in third-party dependencies (report those upstream;
tell us so we can pin/patch), issues that require a misconfiguration explicitly warned
against in the docs, and findings against a deployer's own custom code.

## Hardening references

RufistoWard's threat model is documented per protocol flow in the architecture threat
appendix and is informed by:

- RFC 6819 — OAuth 2.0 Threat Model and Security Considerations
- `draft-ietf-oauth-security-topics` — OAuth 2.0 Security Best Current Practice
- OpenID Connect Core §16 — Security Considerations

Thank you for helping keep RufistoWard and the applications that depend on it safe.
