# Role: Prepares the .NET API and existing Python cutout dependencies for owner-run container hosting.
# Build from the repository root when separately authorized:
#   docker build -f backend/Dockerfile -t nbjewel-api .
# Identity, connections and provider keys come only from the host environment, never the image.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY backend/ backend/
COPY sample-data/ sample-data/
COPY scripts/cutout-jewelry.py scripts/cutout-jewelry.py
RUN dotnet publish backend/NbJewel.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pil python3-numpy python3-scipy \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Render sends traffic to port 10000 by default.
ENV ASPNETCORE_HTTP_PORTS=10000
ENV CUTOUT_PYTHON_PATH=/usr/bin/python3
# A persistent volume must be mounted here by the operator; an image layer is not durable storage.
ENV BUSINESS_STORAGE_PATH=/var/data/businesses
ENV ITEM_ASSET_STORAGE_PATH=/var/data/item-assets
ENV JEWELRY_STORAGE_PATH=/var/data/legacy-jewelry
EXPOSE 10000
ENTRYPOINT ["dotnet", "NbJewel.Api.dll"]
