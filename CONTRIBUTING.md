# Contributing to NBXplorer

## Development environment

NBXplorer requires the .NET 10 SDK. Visual Studio, Visual Studio Code, Rider, and command-line editors can all build the repository.

Build the server from the repository root:

```bash
./build.sh
```

On PowerShell:

```powershell
./build.ps1
```

## Run the tests

Start the test dependencies and run the suite:

```bash
cd NBXplorer.Tests
docker compose up -d dev
dotnet test
```

The `dev` service starts PostgreSQL and the other services required by the test environment. The first run can take several minutes because the tests download node binaries.

Stop the dependencies when finished:

```bash
docker compose down
```

## Add support for another chain

1. Add the chain to [`NBitcoin.Altcoins`](https://github.com/MetacoSA/NBitcoin/tree/master/NBitcoin.Altcoins).
2. Update NBXplorer to a version of `NBitcoin.Altcoins` that contains the chain.
3. Add an `NBXplorerNetworkProvider` implementation, using [Litecoin](NBXplorer.Client/NBXplorerNetworkProvider.Litecoin.cs) as an example.
4. Add the chain's test environment to [`ServerTester.Environment.cs`](NBXplorer.Tests/ServerTester.Environment.cs).
5. Run the test suite.

## Documentation

Keep the root README concise. Operator documentation belongs in [`docs/Operator.md`](docs/Operator.md), API concepts belong in [`docs/API.md`](docs/API.md), and endpoint details belong in the OpenAPI document at [`NBXplorer/wwwroot/api.json`](NBXplorer/wwwroot/api.json).
