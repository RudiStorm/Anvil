FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The store references Anvil projects in the repository, so the build context
# must remain the repository root.
COPY . .

RUN dotnet restore samples/Anvil.Store/Anvil.Store.csproj
RUN dotnet publish samples/Anvil.Store/Anvil.Store.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY samples/Anvil.Store/Container/entrypoint.sh /app/entrypoint.sh

RUN chmod +x /app/entrypoint.sh \
    && mkdir -p /app/data/store-storage \
    && groupadd --system --gid 10001 anvil \
    && useradd --system --uid 10001 --gid anvil --home-dir /app --no-create-home anvil \
    && chown -R anvil:anvil /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

ENTRYPOINT ["/app/entrypoint.sh"]
