# Licensing

RufistoWard is dual-licensed. You choose which side you are on.

## The short version

| You are | Your licence | Cost |
|---------|--------------|------|
| Self-hosting RufistoWard, modified or not | **AGPL-3.0** | Free |
| Running it as part of your own product, and you cannot publish your source | **Commercial** | Talk to us |
| Using the multi-tenancy or tamper-evident audit tier | **Commercial** | Talk to us |

Both licences get **the entire protocol surface**. Every OAuth 2.0 and OpenID Connect
flow, the Admin UI, the IdentityServer4 migration tooling, security event logging, and
every future protocol RufistoWard supports are free under the AGPL, forever, and will
never be moved behind a paywall.

That is the whole reason this project exists. IdentityServer4 was free, then it was not,
and a lot of people found out the hard way. **RufistoWard does not charge for OAuth
flows. It never will.** What it charges for is operational convenience — never protocol.

## Open source: AGPL-3.0

The full text is in [LICENSE](LICENSE). The clause that matters is section 13: if you
modify RufistoWard and let people reach it over a network, your users are entitled to
your modified source.

If you self-host RufistoWard to authenticate your own users, **the AGPL costs you
nothing and asks you for nothing**. Run it, patch it, fork it, keep it internal. The
Affero clause only reaches you when you offer a *modified* version to others over a
network.

The AGPL is here for exactly one reason: it stops a well-funded competitor taking this
core, adding a tenant resolver, and selling it back to the community that built it. It
is a fence around competitors, not around users.

## Commercial

The commercial licence exists for two situations.

**1. The AGPL does not fit your legal reality.** Some organisations refuse AGPL by
policy — sometimes for good reasons, often just because a spreadsheet said so. If your
legal team has rejected the AGPL, that rejection is not the end of the conversation.
It is the start of one. The commercial licence releases you from section 13.

**2. You need the commercial tier.** Some capabilities are not in the AGPL core:

- **Multi-tenancy** — per-tenant signing keys, per-tenant JWKS, issuer and discovery
  routing, tenant provisioning, and the conformance evidence that a token issued to one
  tenant is refused by the next.
- **Tamper-evident audit** — hash-chained, append-only, independently verifiable, with
  retention and export.

Note what is *not* on that list. **Security event logging is free.** Who signed in, what
an admin changed, when a client was deleted — that is security, and security is never
paid. What the commercial tier adds is *evidentiary integrity*: proof the record was not
edited. That is compliance, and compliance is a budget line with an auditor attached.

A log you can quietly rewrite is a diary. An audit trail you can prove was not altered
is evidence.

## Contributing

Contributions are accepted under a Contributor Licence Agreement. Because RufistoWard is
dual-licensed, contributed code has to be usable on **both** sides — otherwise the
project could not ship its own contributions in the commercial build, and a single
merged pull request would put a hole in it that no amount of goodwill could patch later.

The CLA does not take your copyright. You keep it. You grant the maintainer the right to
license your contribution under both the AGPL and the commercial licence. See
[CONTRIBUTING.md](CONTRIBUTING.md).

This is the same arrangement used by Grafana, Mattermost, Bitwarden, and Nextcloud — all
AGPL, all dual-licensed, all still here.

## Questions

Which licence applies to you is a genuine question and sometimes a hard one. Open a
GitHub Discussion and ask. Nobody is going to sue you for asking.
