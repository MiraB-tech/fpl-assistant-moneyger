# Recipe for packing the backend into a container image — a sealed box
# that any server (Render, or your own PC) can run in exactly the same way.
#
# It works in two stages, like a kitchen and a serving plate:
#   1. "build": a big image with every .NET tool turns the C# code into a
#      ready-to-run program.
#   2. "final": only that finished program is copied onto a small, clean
#      image that can run .NET apps but has no build tools. Smaller box,
#      faster start, less for an attacker to work with.
#
# Build it from the backend/ folder:   docker build -t moneyger-api .

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy just the project file first and download its packages. Docker
# remembers ("caches") each step, so as long as the package list hasn't
# changed, later builds skip this slow download and reuse the result.
COPY src/AssistantMoneyger.Api/AssistantMoneyger.Api.csproj src/AssistantMoneyger.Api/
RUN dotnet restore src/AssistantMoneyger.Api/AssistantMoneyger.Api.csproj

# Now copy the rest of the code and compile it into a ready-to-run folder.
COPY src/ src/
RUN dotnet publish src/AssistantMoneyger.Api/AssistantMoneyger.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---------- Stage 2: final ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The backend listens on port 8080 inside the box. (The host maps its own
# public address onto this — we tell Render the port when we set it up.)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the image's built-in ordinary user, not as the all-powerful
# "root" admin, so a bug in the app can't be used to take over the box.
USER $APP_UID

ENTRYPOINT ["dotnet", "AssistantMoneyger.Api.dll"]
