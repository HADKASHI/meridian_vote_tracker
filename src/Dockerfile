# One backend image, three entry points: the migration bundle, the API and the worker.
# Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore
COPY src/ src/
RUN dotnet publish src/Meridian.Api -c Release -o /out/api -p:OpenApiGenerateDocumentsOnBuild=false \
 && dotnet publish src/Meridian.Worker -c Release -o /out/worker \
 && dotnet ef migrations bundle --configuration Release --project src/Meridian.Data --startup-project src/Meridian.Data \
      -o /out/migrate/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl only for the API's health check; aspnet:10.0 ships without curl or wget.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /out /app
USER $APP_UID
ENTRYPOINT ["dotnet", "/app/api/Meridian.Api.dll"]
