# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/TruckingApi/TruckingApi.csproj backend/TruckingApi/
RUN dotnet restore backend/TruckingApi/TruckingApi.csproj

COPY backend/ backend/
RUN dotnet publish backend/TruckingApi/TruckingApi.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=build /app/publish .
COPY frontend/ wwwroot/

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TruckingApi.dll"]
