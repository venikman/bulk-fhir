FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY BulkFhir.slnx .
COPY src/BulkFhir.Domain/BulkFhir.Domain.fsproj src/BulkFhir.Domain/
COPY src/BulkFhir.Storage/BulkFhir.Storage.fsproj src/BulkFhir.Storage/
COPY src/BulkFhir.Api/BulkFhir.Api.fsproj src/BulkFhir.Api/
COPY src/BulkFhir.Import/BulkFhir.Import.fsproj src/BulkFhir.Import/
COPY tests/BulkFhir.Tests.E2E/BulkFhir.Tests.E2E.fsproj tests/BulkFhir.Tests.E2E/
RUN dotnet restore

COPY . .
RUN dotnet publish src/BulkFhir.Api/BulkFhir.Api.fsproj -c Release -o /app/api
RUN dotnet publish src/BulkFhir.Import/BulkFhir.Import.fsproj -c Release -o /app/import

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/api ./api
COPY --from=build /app/import ./import

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "api/BulkFhir.Api.dll"]
