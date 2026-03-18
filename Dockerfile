FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY BulkFhir.slnx .
COPY src/BulkFhir.Domain/BulkFhir.Domain.fsproj src/BulkFhir.Domain/
COPY src/BulkFhir.Storage/BulkFhir.Storage.fsproj src/BulkFhir.Storage/
COPY src/BulkFhir.Api/BulkFhir.Api.fsproj src/BulkFhir.Api/
COPY tests/BulkFhir.Tests.E2E/BulkFhir.Tests.E2E.fsproj tests/BulkFhir.Tests.E2E/
RUN dotnet restore

COPY . .
RUN dotnet publish src/BulkFhir.Api/BulkFhir.Api.fsproj -c Release -o /app/api

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/api ./api

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "api/BulkFhir.Api.dll"]
