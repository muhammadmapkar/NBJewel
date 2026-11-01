# Role: Builds and runs the .NET API (with the try-on jewelry endpoints) in a container, for
# hosts like Render that deploy from a Dockerfile. Build from the repository root:
#   docker build -f backend/Dockerfile -t nbjewel-api .
# Settings come from the host's environment variables (OWNER_PIN, CORS_ORIGINS, ...), never here.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/ backend/
COPY sample-data/ sample-data/
RUN dotnet publish backend/NbJewel.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render sends traffic to port 10000 by default.
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "NbJewel.Api.dll"]
