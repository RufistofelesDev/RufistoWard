# RufistoWard OP image — used by the conformance harness (tests/conformance/) and
# any container deployment. Multi-stage .NET 10 build. Build context = repo root:
#   docker build -f src/RufistoWard.Host/Dockerfile -t rufistoward-op .
#
# Tailwind CSS is intentionally NOT built here: the BuildTailwindCss target is
# conditional on tools/tailwindcss existing (absent in this image), so it is
# skipped. The conformance suite drives the /Login form by element id, not by
# styling, so missing site.css is cosmetic. For a styled production image, add a
# Tailwind download + run step before `dotnet publish`.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central build/package management lives at the repo root — copy first for layer caching.
COPY Directory.Build.props Directory.Packages.props RufistoWard.slnx ./

# Project files for restore (copy the whole src/tests tree's csproj graph the Host depends on).
COPY src/RufistoWard.Domain/RufistoWard.Domain.csproj          src/RufistoWard.Domain/
COPY src/RufistoWard.Application/RufistoWard.Application.csproj  src/RufistoWard.Application/
COPY src/RufistoWard.Infrastructure/RufistoWard.Infrastructure.csproj src/RufistoWard.Infrastructure/
COPY src/RufistoWard.Host/RufistoWard.Host.csproj               src/RufistoWard.Host/
RUN dotnet restore src/RufistoWard.Host/RufistoWard.Host.csproj

# Full source + publish.
COPY src/ src/
RUN dotnet publish src/RufistoWard.Host/RufistoWard.Host.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Kestrel HTTPS + URLs are supplied by the harness via env (ASPNETCORE_URLS,
# ASPNETCORE_Kestrel__Certificates__Default__*). See tests/conformance/.
EXPOSE 5001
ENTRYPOINT ["dotnet", "RufistoWard.Host.dll"]
