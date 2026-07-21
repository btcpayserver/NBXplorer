FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS builder
WORKDIR /source
COPY NBXplorer/NBXplorer.csproj NBXplorer/NBXplorer.csproj
COPY NBXplorer.Client/NBXplorer.Client.csproj NBXplorer.Client/NBXplorer.Client.csproj
# Cache some dependencies
RUN cd NBXplorer && dotnet restore && cd ..
COPY . .
RUN cd NBXplorer && \
    dotnet publish --output /app/ --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN mkdir /datadir
ENV NBXPLORER_DATADIR=/datadir
VOLUME /datadir

COPY --from=builder "/app" .
ENTRYPOINT ["dotnet", "NBXplorer.dll"]
