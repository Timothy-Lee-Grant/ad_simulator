FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /build

COPY src/BidEngine/BidEngine.csproj ./src/BidEngine/

COPY src/Shared/ ./src/Shared/

RUN dotnet restore src/BidEngine/BidEngine.csproj

COPY src/BidEngine/ ./src/BidEngine/

RUN dotnet build -c Release src/BidEngine/BidEngine.csproj

COPY src/BidEngine/model/ ./src/BidEngine/model/

RUN dotnet publish  -c Release -o /app src/BidEngine/BidEngine.csproj

FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=builder /app .

COPY src/BidEngine/model/ ./model/

EXPOSE 8081

ENTRYPOINT [ "dotnet", "BidEngine.dll"]