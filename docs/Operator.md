# Operator guide

This guide covers installing and configuring NBXplorer. For wallet and API concepts, see the [API integrator guide](API.md).

> [!WARNING]
> Do not expose NBXplorer directly to the internet. By default it listens on loopback and uses cookie authentication. Keep it on a trusted network; if remote access is required, provide authentication and TLS through a properly secured application or reverse proxy.

## Architecture and requirements

An NBXplorer deployment requires:

* NBXplorer
* PostgreSQL 13 or later
* A synchronized Bitcoin Core 24.0 or later node

Building NBXplorer from source also requires the .NET 10 SDK. A pruned Bitcoin node is supported because NBXplorer indexes only registered wallets and addresses.

PostgreSQL is mandatory. NBXplorer creates its database when needed and applies database migrations during startup.

## Docker

The published image is [`nicolasdorier/nbxplorer`](https://hub.docker.com/r/nicolasdorier/nbxplorer/).

The container must be able to reach both PostgreSQL and the node's RPC and P2P endpoints. Configure it with `NBXPLORER_` environment variables, for example:

```yaml
environment:
  NBXPLORER_NETWORK: mainnet
  NBXPLORER_CHAINS: btc
  NBXPLORER_BIND: 0.0.0.0:24444
  NBXPLORER_POSTGRES: User ID=postgres;Password=change-me;Host=postgres;Port=5432;Database=nbxplorer
  NBXPLORER_BTCRPCURL: http://bitcoind:8332/
  NBXPLORER_BTCNODEENDPOINT: bitcoind:8333
```

Publish port `24444` only to a trusted application network. Supply node RPC authentication through its cookie file or the corresponding `NBXPLORER_BTCRPCUSER` and `NBXPLORER_BTCRPCPASSWORD` variables. Do not use `NBXPLORER_NOAUTH=1` outside an isolated development environment.

The repository's `docker-compose.regtest.yml` is an old regtest development sample, not a complete deployment: it does not provide the required PostgreSQL service or connection string.

## Build and run from source

Build the server from the repository root:

```bash
./build.sh
```

On PowerShell:

```powershell
./build.ps1
```

Run NBXplorer and print its available options:

```bash
./run.sh --help
```

On PowerShell:

```powershell
./run.ps1 --help
```

For example, this starts Bitcoin and Litecoin support on regtest with a local PostgreSQL server:

```bash
./run.sh --chains=btc,ltc --network=regtest \
  --postgres="User ID=postgres;Host=127.0.0.1;Port=5432;Database=nbxplorer"
```

To invoke the project directly, place NBXplorer arguments after `--`:

```bash
dotnet run --no-launch-profile --project NBXplorer/NBXplorer.csproj -- --help
```

To run a compiled server, pass its DLL to the .NET runtime:

```bash
dotnet NBXplorer.dll --help
```

## Configuration

NBXplorer accepts settings from command-line arguments, environment variables, or a configuration file. Run `./run.sh --help` or `./run.ps1 --help` for the current option list.

The same setting can be expressed in each form:

| Source | Example |
| --- | --- |
| Command line | `--chains=btc,ltc` |
| Environment | `NBXPLORER_CHAINS=btc,ltc` |
| Configuration file | `chains=btc,ltc` |

Environment variables use the `NBXPLORER_` prefix. Chain-specific settings use the chain code, such as `NBXPLORER_BTCRPCURL` for `btc.rpc.url`.

The default configuration file is:

* Windows: `%APPDATA%\NBXplorer\<Network>\settings.config`
* Linux and macOS: `~/.nbxplorer/<Network>/settings.config`

The network folder is `Main`, `TestNet`, or `RegTest`. Change the configuration file with `--conf=<path>`.

By default NBXplorer uses mainnet, enables only BTC, listens on `127.0.0.1:24444`, and expects the node at its standard local endpoints.

### Node authentication

NBXplorer uses the node's RPC cookie by default when the node uses its standard data directory. If the node uses explicit RPC credentials, configure them for each enabled chain:

```text
btc.rpc.url=http://127.0.0.1:8332/
btc.rpc.user=rpc-user
btc.rpc.password=rpc-password
btc.node.endpoint=127.0.0.1:8333
```

The RPC endpoint and P2P node endpoint are different settings. NBXplorer uses RPC for node queries and broadcasting, and the P2P connection to receive transaction and block announcements.

### PostgreSQL

Set the PostgreSQL connection string with `--postgres`, `NBXPLORER_POSTGRES`, or `postgres=` in the configuration file:

```text
postgres=User ID=postgres;Password=change-me;Host=127.0.0.1;Port=5432;Database=nbxplorer
```

See the [Npgsql connection string reference](https://www.npgsql.org/doc/connection-string-parameters.html) for additional options and the [NBXplorer schema guide](Postgres-Schema.md) for direct database access.

## Start height and rescanning

On its first run, NBXplorer starts scanning a chain from the configured `<chain>.startheight`. The default value, `-1`, uses the node's current blockchain height. Transactions before that height are not discovered automatically.

To discover older transactions, stop NBXplorer and restart it with a suitable start height and the chain's rescan option. For example:

```bash
./run.sh --chains=btc --btcstartheight=800000 --btcrescan
```

Choose the earliest height that may contain activity for the wallets you intend to track. A pruned node can rescan only blocks it still retains; use an archival node or choose a height within the node's retained block range. Rescanning a large range takes additional time and node resources.

## Supported chains

Bitcoin is NBXplorer's primary supported chain. The server also contains network definitions for Liquid and these Bitcoin-derived chains:

* Althash
* Argoneum
* Bitcoin Cash (`BCH`)
* Bitcoin Gold (`BTG`)
* Bitcore
* Chaincoin
* ColossusXT
* Dash
* Dogecoin
* Feathercoin
* GoByte
* Groestlcoin
* Litecoin
* Monacoin
* MonetaryUnit
* Monoeci
* Pepecoin
* Polis
* Qtum
* Terracoin
* UFO
* Viacoin

Enable chains with the `chains` setting. Support quality and upstream node compatibility vary across alternative chains; Bitcoin remains the primary development and testing target.
