FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /build

COPY src/BidEngine/BidEngine.csproj ./src/BidEngine/

COPY src/Shared/ ./src/Shared/

RUN dotnet restore src/BidEngine/BidEngine.csproj

COPY src/BidEngine/ ./src/BidEngine/

RUN dotnet build -c Release src/BidEngine/BidEngine.csproj

RUN dotnet publish  -c Release -o /app src/BidEngine/BidEngine.csproj

FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=builder /app .

# Create a mountable model directory so users can provide model files by
# mounting a host directory (e.g., -v ./model:/app/model) or via build-time
# copy. We also install curl/unzip so images can fetch model artifacts at run-time if desired.
RUN apt-get update && apt-get install -y curl unzip && rm -rf /var/lib/apt/lists/* \
	&& mkdir -p /app/model

VOLUME ["/app/model"]

EXPOSE 8081

ENTRYPOINT [ "dotnet", "BidEngine.dll"]