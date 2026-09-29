# NBXplorer documentation

NBXplorer documentation is organized by audience.

## Run NBXplorer

The [operator guide](Operator.md) covers:

* System requirements and deployment security
* Docker and source installation
* Node and PostgreSQL configuration
* Configuration files, command-line arguments, and environment variables
* Initial synchronization, start heights, and rescanning
* Supported chains

## Integrate with NBXplorer

The [API integrator guide](API.md) covers:

* The wallet tracking workflow
* Tracked sources, derivation schemes, groups, and standalone addresses
* Standard and policy-based derivation schemes
* API authentication
* Official and community client libraries

Use the [REST API reference](https://btcpayserver.github.io/NBXplorer/) for endpoint and schema details.

## Database reference

The [PostgreSQL schema guide](Postgres-Schema.md) describes the database model, views, and functions available for direct queries.

The REST API is the stable integration boundary. Applications that query the database directly are coupled to its schema and should review schema changes when upgrading NBXplorer.

## Contribute

See [CONTRIBUTING.md](../CONTRIBUTING.md) to build NBXplorer, run its tests, or add support for another chain.
