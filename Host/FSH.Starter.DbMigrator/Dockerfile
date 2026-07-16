# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore Host/FSH.Starter.DbMigrator/FSH.Starter.DbMigrator.csproj
RUN dotnet publish Host/FSH.Starter.DbMigrator/FSH.Starter.DbMigrator.csproj \
    -c Release \
    --no-restore \
    -o /publish

FROM mcr.microsoft.com/dotnet/nightly/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /publish .

USER $APP_UID
ENV DOTNET_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "FSH.Starter.DbMigrator.dll"]
CMD ["apply"]
