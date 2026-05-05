# ── Stage 1: Build ───────────────────────────────────────────────────────────
# Use the official .NET 8 SDK image to compile the code
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (faster rebuilds)
COPY JsbaiBackend.csproj .
RUN dotnet restore

# Copy all source code
COPY . .

# Compile and publish a release build
RUN dotnet publish -c Release -o /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
# Use a smaller runtime-only image (no compiler needed to run the app)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create the uploads folder
RUN mkdir -p wwwroot/uploads

# Copy the compiled app from the build stage
COPY --from=build /app/publish .

# Railway assigns a PORT environment variable — we need to listen on it
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

EXPOSE 8080

ENTRYPOINT ["dotnet", "JsbaiBackend.dll"]
