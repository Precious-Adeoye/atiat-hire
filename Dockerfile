# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AtiatHire.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
# Render provides $PORT (10000 by default). Bind to it on all interfaces.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet AtiatHire.dll"]
