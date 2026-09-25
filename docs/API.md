# API integrator guide

NBXplorer is a lightweight, multi-wallet UTXO tracker. It listens to transactions and blocks from a trusted full node and indexes only the wallets and addresses that an application registers.

This guide explains the domain model and integration flow. Use the [REST API reference](https://btcpayserver.github.io/NBXplorer/) for endpoint paths, request bodies, and response schemas. See the [operator guide](Operator.md) to install and configure the server.

## Integration workflow

A typical integration follows this lifecycle:

1. Register a derivation scheme, standalone address, or group.
2. Request and reserve the next unused address when receiving a payment.
3. Consume transaction and block events through polling, long polling, or WebSockets.
4. Query the tracked source's transactions, balances, and UTXOs.
5. Create a PSBT for the application or hardware wallet to sign.
6. Submit the signed transaction for broadcast.

These operations are grouped by tracked-source type in the [REST API reference](https://btcpayserver.github.io/NBXplorer/).

## Tracked sources

A tracked source identifies a set of scripts and the transactions, UTXOs, and balances associated with them. NBXplorer supports three tracked-source types:

* Derivation schemes for deterministic wallets
* Groups for combining tracked sources and individual addresses
* Standalone addresses

Their serialized forms are:

```text
DERIVATIONSCHEME:<derivation-scheme>
GROUP:<group-id>
ADDRESS:<address>
```

## Derivation scheme

A derivation scheme, called a `derivationStrategy` in parts of the API, describes how NBXplorer derives and tracks a deterministic wallet's scripts. NBXplorer tracks deposit paths (`0/i`), change paths (`1/i`), and the direct path (`i`) for standard schemes.

Never send an extended private key to NBXplorer. Tracking requires public wallet information only; keep signing keys in the application or signing device.

### Standard derivation schemes

Standard schemes cover common single-signature and multisignature wallets.

| Address type | Format |
| --- | --- |
| P2WPKH | `xpub...` |
| P2SH-P2WPKH | `xpub...-[p2sh]` |
| P2PKH | `xpub...-[legacy]` |
| P2TR | `xpub...-[taproot]` |
| Multisig P2WSH | `2-of-xpub1...-xpub2...` |
| Multisig P2SH-P2WSH | `2-of-xpub1...-xpub2...-[p2sh]` |
| Multisig P2SH | `2-of-xpub1...-xpub2...-[legacy]` |

NBXplorer sorts multisig public keys before generating addresses by default. Add `-[keeporder]` to preserve their supplied order. Options can be combined, for example:

```text
2-of-xpub1...-xpub2...-[legacy]-[keeporder]
```

Most routes also include a `cryptoCode`, such as `BTC` or `LTC`, to identify the chain.

### Policy derivation schemes

Policy schemes describe more complex spending conditions with [Miniscript](https://github.com/bitcoin/bips/blob/master/bip-0379.md), [output descriptors](https://github.com/bitcoin/bips/blob/master/bip-0380.mediawiki), and [wallet policies](https://github.com/bitcoin/bips/blob/master/bip-0388.mediawiki).

For example, this policy requires key A and either key B, key C, or a relative timelock:

```text
and(pk(A),or(pk(B),or(pk(C),older(1000))))
```

Its Miniscript can be wrapped as P2WSH:

```text
wsh(and_v(or_c(pk(B),or_c(pk(C),v:older(1000))),pk(A)))
```

Replace each key placeholder with public key origin information and an account-level extended public key:

```text
[fingerprint/derivation/path]xpub.../**
```

The `/**` suffix defined by [BIP 389](https://github.com/bitcoin/bips/blob/master/bip-0389.mediawiki) derives deposit addresses from `0/i` and change addresses from `1/i`. A different multipath expression, such as `<2;3>/*`, can be used when required by the wallet.

Derivation schemes are part of API URL paths and must be URL encoded by the HTTP client. In particular, encode brackets, slashes, parentheses, commas, apostrophes, colons, and asterisks. Do not place private key material in a derivation scheme or URL.

## Groups

A group combines multiple tracked sources into one logical tracked source. It can contain derivation schemes, standalone addresses, and other groups. Scripts and their related transactions and UTXOs flow from child tracked sources into the parent group.

Groups may be nested, but cannot contain cycles. Avoid adding an excessive number of children when an application frequently retrieves the full group because the response includes its children.

## Standalone addresses

A standalone address tracks one address without a derivation scheme. It behaves like other tracked sources for transaction and UTXO queries and is represented as `ADDRESS:<address>`.

## Authentication

NBXplorer listens on loopback and enables cookie authentication by default. It writes a new credential to `.cookie` in the network-specific data directory when it starts:

* Windows: `%APPDATA%\NBXplorer\<Network>\.cookie`
* Linux and macOS: `~/.nbxplorer/<Network>/.cookie`

The network folder is `Main`, `TestNet`, or `RegTest`. The file contains a `username:password` credential for HTTP Basic Authentication.

For example, query the mainnet fee endpoint from the same Unix-like host:

```bash
curl --user "$(cat "$HOME/.nbxplorer/Main/.cookie")" \
  http://127.0.0.1:24444/v1/cryptos/btc/fees/3
```

The response has this form:

```json
{
  "feeRate": 9,
  "blockCount": 3
}
```

The cookie changes each time NBXplorer starts. Reload it before reconnecting if a request returns `401 Unauthorized`.

Authentication can be disabled with `--noauth` or `NBXPLORER_NOAUTH=1` for an isolated local development environment. Do not disable it on a shared or untrusted network, and do not expose NBXplorer directly to the internet.

## Client libraries

### .NET

[`NBXplorer.Client`](https://www.nuget.org/packages/NBxplorer.Client) is the official .NET client. Its `ExplorerClient` API covers tracked sources, unused addresses, balances, UTXOs, transactions, events, groups, rescans, PSBT creation, and broadcasting.

The repository includes a [multisig example](../Examples/MultiSig/Program.cs) and integration tests in [`NBXplorer.Tests`](../NBXplorer.Tests/) that demonstrate additional client operations.

### Node.js

[`NBXplorer.NodeJS`](https://github.com/junderw/NBXplorer.NodeJS) is a community-maintained client. It is not maintained or compatibility-tested by the NBXplorer project; verify that it supports the NBXplorer version used by your deployment.

## Direct PostgreSQL access

Applications can query NBXplorer's database directly when the REST API does not expose a required report or aggregate. See the [PostgreSQL schema guide](Postgres-Schema.md).

Direct SQL integrations are coupled to the database schema. Prefer the REST API when possible and review schema changes before upgrading NBXplorer.
