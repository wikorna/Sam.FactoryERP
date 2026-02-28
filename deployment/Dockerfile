# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# ---- OS packages (keep minimal) ----
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    fontconfig \
    curl \
    && rm -rf /var/lib/apt/lists/*

# ---- Create non-root user ----
RUN useradd -m -u 10001 appuser

# ---- Workdir ----
WORKDIR /app

# ---- Copy published output (from Windows publish) ----
# deployment/publish is created by build-offline-staging.ps1
COPY publish/ ./

# ---- Optional: Install extra .deb deps if present ----
# deployment/docker-deps/*.deb copied by build script
COPY docker-deps/ /tmp/docker-deps/
RUN if ls /tmp/docker-deps/*.deb 1>/dev/null 2>&1; then \
      dpkg -i /tmp/docker-deps/*.deb || apt-get update && apt-get install -f -y && rm -rf /var/lib/apt/lists/*; \
    else \
      echo "No .deb deps found. Skipping."; \
    fi && rm -rf /tmp/docker-deps

# ---- Copy assets: Fonts & Reports (optional) ----
COPY Sources/ /app/Sources/

# ---- Copy Licenses (optional) ----
COPY Licenses/ /app/Licenses/

# ---- Copy CA certs (optional) ----
COPY ca-certificates/ /https/

# ---- Entrypoint ----
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# ---- Trust CA certs if provided ----
# docker-entrypoint.sh can also do this, but doing once at build-time helps deterministic images.
RUN if ls /https/*.crt 1>/dev/null 2>&1; then \
      cp /https/*.crt /usr/local/share/ca-certificates/ && update-ca-certificates; \
    else \
      echo "No extra CA certs provided at build-time."; \
    fi

# ---- Environment ----
ENV ASPNETCORE_URLS=http://0.0.0.0:5000 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Asia/Bangkok

EXPOSE 5000

# ---- Healthcheck ----
# Adjust path/port to your real health endpoint
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -fsS http://127.0.0.1:5000/health || exit 1

# ---- Switch to non-root ----
USER appuser

# ---- Start ----
# IMPORTANT: replace API.dll with your actual published dll name
ENTRYPOINT ["/app/docker-entrypoint.sh"]
CMD ["dotnet", "API.dll"]
