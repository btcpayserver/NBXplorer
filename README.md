# NBXplorer

[![NuGet](https://img.shields.io/nuget/v/NBxplorer.Client.svg)](https://www.nuget.org/packages/NBxplorer.Client)
[![Docker pulls](https://img.shields.io/docker/pulls/nicolasdorier/nbxplorer.svg)](https://hub.docker.com/r/nicolasdorier/nbxplorer/)
[![CI](https://github.com/btcpayserver/NBXplorer/actions/workflows/ci.yml/badge.svg)](https://github.com/btcpayserver/NBXplorer/actions/workflows/ci.yml)

NBXplorer is a minimalist Bitcoin UTXO tracker for HD wallets. It connects to a trusted full node, watches only the wallets and addresses you register, and exposes their transactions, UTXOs, and balances through a REST API.

NBXplorer supports P2PKH, P2SH, SegWit, Taproot, multisig, and [wallet policies](https://github.com/bitcoin/bips/blob/master/bip-0388.mediawiki). It works with pruned nodes, stores its index in PostgreSQL, and can track many wallets without importing them into Bitcoin Core.

> [!WARNING]
> NBXplorer is an infrastructure service and is not intended to be exposed directly to the internet. Keep it on a trusted network and place authentication and transport security at your application boundary.

## Documentation

Start at the [documentation index](docs/README.md), or go directly to:

* [Operator guide](docs/Operator.md) for installation, configuration, supported chains, and rescanning
* [API integrator guide](docs/API.md) for tracked sources, derivation schemes, authentication, and client libraries
* [REST API reference](https://btcpayserver.github.io/NBXplorer/) for endpoints and schemas
* [PostgreSQL schema](docs/Postgres-Schema.md) for direct database access
* [Contributing](CONTRIBUTING.md) for building, testing, and adding chain support

## Typical workflow

1. Track a wallet derivation scheme, a standalone address, or a group of tracked sources.
2. Request the next unused address when receiving a payment.
3. Consume transaction and block events through polling, long polling, or WebSockets.
4. Query transactions, balances, and UTXOs.
5. Create a PSBT for your application to sign, then broadcast the signed transaction.

The [API integrator guide](docs/API.md) explains these concepts, and the [REST API reference](https://btcpayserver.github.io/NBXplorer/) documents the individual operations.

## When to use NBXplorer

NBXplorer is designed for services that need private, self-hosted wallet tracking without indexing the entire blockchain.

Compared with an Electrum server, NBXplorer can run on a pruned node, supports many wallets, and provides wallet-oriented APIs without requiring the Electrum protocol. Compared with Bitcoin Core wallets, it keeps wallet tracking separate from the node and is designed to handle large numbers of addresses and wallets. Unlike a hosted API, it does not require sharing financial activity with a third party.

Bitcoin is the primary supported chain. NBXplorer can also index Liquid and several Bitcoin-derived networks; see the [operator guide](docs/Operator.md#supported-chains).

## License

NBXplorer is licensed under the [MIT License](LICENSE).
