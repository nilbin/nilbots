# Bot Arena images. `runtime` serves web/match/migrate roles without a compiler;
# `compiler` adds the pinned C#→WASM toolchain and privileged launcher needed to
# drop submission builds into the unprivileged botbuild account.

FROM ubuntu:24.04 AS web
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM ubuntu:24.04 AS toolchain
RUN apt-get update && apt-get install -y --no-install-recommends \
      curl ca-certificates git util-linux clang-18 llvm-18 lld-18 wasi-libc \
      libclang-rt-18-dev-wasm32 libc++-18-dev-wasm32 libc++abi-18-dev-wasm32 \
    && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

# Submission compilation launches as this account with setpriv and ulimits.
RUN useradd --system --create-home --home-dir /var/lib/botbuild --shell /usr/sbin/nologin botbuild

WORKDIR /app
COPY . .
RUN rm -rf web && bash scripts/setup-wasi-sdk.sh
RUN dotnet build BotArena.sln -c Release -v q
RUN bash scripts/build-wasm-guest.sh
# Prime the exact packages used by controlled player builds. The named
# botbuildhome volume copies this cache on first use.
RUN cd /tmp && dotnet run --project /app/src/BotArena.Cli -c Release -- new PrimeBot \
    && BOTARENA_ROOT=/app WASI_SDK_PATH=/opt/botarena/wasi-sdk-29.0 \
       dotnet run --project /app/src/BotArena.Cli -c Release -- build /tmp/PrimeBot \
    && rm -rf /tmp/PrimeBot
RUN dotnet publish src/BotArena.App -c Release --no-restore -o /app/publish
COPY --from=web /src/web/dist /app/web/dist

FROM toolchain AS compiler
RUN mkdir -p /data
ENV BOTARENA_ROOT=/app \
    BOTARENA_DATA=/data \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
VOLUME /data
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1
CMD ["dotnet", "/app/publish/BotArena.App.dll"]

FROM ubuntu:24.04 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /opt/dotnet \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh \
    && useradd --system --uid 1654 --create-home --home-dir /home/botarena \
       --shell /usr/sbin/nologin botarena \
    && mkdir -p /app /data \
    && chown botarena:botarena /data
WORKDIR /app
COPY --from=toolchain /app/publish /app/publish
COPY --from=toolchain /app/maps /app/maps
COPY --from=toolchain /app/champions /app/champions
COPY --from=toolchain /app/artifacts/wasm /app/artifacts/wasm
COPY --from=toolchain /app/web/dist /app/web/dist
ENV BOTARENA_ROOT=/app \
    BOTARENA_DATA=/data \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_EnableDiagnostics=0
VOLUME /data
EXPOSE 8080
USER botarena
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1
CMD ["dotnet", "/app/publish/BotArena.App.dll"]
