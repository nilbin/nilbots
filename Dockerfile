# nilbots images. `runtime` serves web/match/migrate/coordinator roles without
# a compiler; `compiler` adds the pinned C#→WASM toolchain and runs entirely as
# the unprivileged botbuild account. `pgbouncer` builds the official, checksummed
# PgBouncer source into the small proxy image used by the primary node.

FROM alpine:3.23@sha256:fd791d74b68913cbb027c6546007b3f0d3bc45125f797758156952bc2d6daf40 AS pgbouncer-build
ARG PGBOUNCER_VERSION=1.25.2
ARG PGBOUNCER_SHA256=924ad35113fd0a71c8e2dbe85b5d03445532e2b7b37a9f8a48983beea238b332
RUN apk add --no-cache \
      build-base ca-certificates c-ares-dev curl libevent-dev linux-headers \
      openssl-dev pkgconf \
    && curl -fsSLo /tmp/pgbouncer.tar.gz \
      "https://www.pgbouncer.org/downloads/files/${PGBOUNCER_VERSION}/pgbouncer-${PGBOUNCER_VERSION}.tar.gz" \
    && echo "${PGBOUNCER_SHA256}  /tmp/pgbouncer.tar.gz" | sha256sum -c - \
    && mkdir /tmp/pgbouncer \
    && tar -xzf /tmp/pgbouncer.tar.gz -C /tmp/pgbouncer --strip-components=1 \
    && cd /tmp/pgbouncer \
    && ./configure --prefix=/usr --with-cares --with-openssl \
    && make -j"$(getconf _NPROCESSORS_ONLN)" pgbouncer \
    && install -D -m 755 pgbouncer /out/usr/bin/pgbouncer

FROM alpine:3.23@sha256:fd791d74b68913cbb027c6546007b3f0d3bc45125f797758156952bc2d6daf40 AS pgbouncer
RUN apk add --no-cache ca-certificates c-ares libevent openssl postgresql-client tini \
    && addgroup -S -g 1654 pgbouncer \
    && adduser -S -D -H -u 1654 -G pgbouncer pgbouncer \
    && mkdir -p /etc/pgbouncer \
    && chown pgbouncer:pgbouncer /etc/pgbouncer
COPY --from=pgbouncer-build /out/usr/bin/pgbouncer /usr/bin/pgbouncer
USER pgbouncer
EXPOSE 6432
ENTRYPOINT ["/sbin/tini", "--"]
CMD ["/usr/bin/pgbouncer", "/etc/pgbouncer/pgbouncer.ini"]

FROM ubuntu:24.04 AS web
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
COPY docs/PLAYER-GUIDE.md /src/docs/PLAYER-GUIDE.md
COPY scripts/generate-atlas-variants.mjs scripts/build-cli-viewers.mjs /src/scripts/
RUN npm run build

# Just the frozen mind artifacts the seeders load, without the 400 MB of
# sources, sheets, replays and preflight records beside them.
#
# Pruned by rule rather than by a list of directories. The playlist history is
# immutable, so every ambush-warren-11-stock-v* ever minted stays referenced by
# the seeder, and a hand-maintained COPY list would be one forgotten line away
# from a migration that cannot find its own frozen artifact. `find` picks up
# the next mint on its own.
FROM ubuntu:24.04 AS arena-artifacts
COPY arena-bots/arc-relay /prune/arena-bots/arc-relay
RUN find /prune -type f ! -name bot.wasm -delete \
    && find /prune -type d -empty -delete

# The ordinary hosted app needs the .NET SDK to publish, but it does not need
# clang/LLVM, wasi-sdk, a rebuilt first-party guest, or a primed submission
# cache. Keep this stage independent from `toolchain`: a local/runtime image
# review must consume the repository's committed WASM artifacts exactly as
# checked in. Controlled custom-mind/compiler images still use the explicit
# `compiler` target below.
FROM ubuntu:24.04 AS app-build
RUN apt-get update && apt-get install -y --no-install-recommends \
      curl ca-certificates libicu74 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh
WORKDIR /app
COPY . .
RUN rm -rf web \
    && dotnet publish src/BotArena.App -c Release -o /app/publish

FROM ubuntu:24.04 AS toolchain
RUN apt-get update && apt-get install -y --no-install-recommends \
      curl ca-certificates git util-linux clang-18 llvm-18 lld-18 wasi-libc \
      libclang-rt-18-dev-wasm32 libc++-18-dev-wasm32 libc++abi-18-dev-wasm32 \
    && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

# The final compiler image and every submission build run as this account.
RUN useradd --system --uid 1654 --create-home --home-dir /var/lib/botbuild \
      --shell /usr/sbin/nologin botbuild

WORKDIR /app
COPY . .
RUN rm -rf web && bash scripts/setup-wasi-sdk.sh
RUN dotnet build BotArena.sln -c Release -v q
RUN bash scripts/build-wasm-guest.sh
# Prime the exact packages used by controlled player builds. The named
# offline feed and global package cache are baked into the final compiler.
RUN cd /tmp && dotnet run --project /app/src/BotArena.Cli -c Release -- new PrimeBot \
    && BOTARENA_ROOT=/app BOTARENA_BUILD_ISOLATION=off \
       WASI_SDK_PATH=/opt/botarena/wasi-sdk-29.0 \
       dotnet run --project /app/src/BotArena.Cli -c Release -- build /tmp/PrimeBot \
    && rm -rf /tmp/PrimeBot
RUN dotnet publish src/BotArena.App -c Release --no-restore -o /app/publish
RUN mkdir -p /opt/botarena/toolchain-libs /opt/botarena/nuget-feed \
    && cp /app/src/BotArena.Sdk/bin/Release/net10.0/BotArena.Sdk.dll \
          /opt/botarena/toolchain-libs/ \
    && cp /app/src/BotArena.Guest/bin/Release/net10.0/BotArena.Guest.dll \
          /opt/botarena/toolchain-libs/ \
    && find /root/.nuget/packages -name '*.nupkg' -type f \
       -exec sh -c 'for package do ln -sf "$package" "/opt/botarena/nuget-feed/$(basename "$package")"; done' sh {} + \
    && chmod o+x /root \
    && chmod -R a+rX /root/.nuget /opt/botarena

FROM toolchain AS compiler
# `/data` is owned here for the same reason the runtime stage owns it: this
# stage also serves the app (see CMD below), and without a writable data root
# BOTARENA_DATA falls back to <cwd>/var — /app/var, which this non-root user
# cannot create, so the container died on startup generating its OpenIddict
# development certificate. Creating it in the image also fixes ownership of
# the named volume mounted over it, which Docker seeds from the image path.
RUN mkdir -p /compiler-ipc /work /data \
    && chown botbuild:botbuild /compiler-ipc /work /data
# The site, not just the API. This stage serves the app for the local
# `docker compose` path, and the SPA lookup fails *silently* when the files
# are absent — index.html simply is not there, so no static middleware is
# registered and every page 404s while /api keeps answering. Only the runtime
# stage used to copy this, which made the single-container local deployment
# look like a broken backend rather than a missing frontend.
COPY --from=web /src/web/dist /app/web/dist
ENV BOTARENA_ROOT=/app \
    BOTARENA_DATA=/data \
    BOTARENA_HOME=/work \
    BOTARENA_TOOLCHAIN_LIBS=/opt/botarena/toolchain-libs \
    BOTARENA_NUGET_CONFIG=/app/docker/nuget.offline.config \
    BOTARENA_BUILD_ISOLATION=off \
    NUGET_PACKAGES=/root/.nuget/packages \
    HOME=/work/home \
    DOTNET_CLI_HOME=/work/dotnet \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_EnableDiagnostics=0
USER botbuild
# This build must succeed without a network and without root. It proves that
# every compiler/NuGet input required by public submissions is baked in.
RUN --network=none cd /work \
    && dotnet /app/src/BotArena.Cli/bin/Release/net10.0/botarena.dll new OfflineSmoke \
    && dotnet /app/src/BotArena.Cli/bin/Release/net10.0/botarena.dll build /work/OfflineSmoke \
    && find /work -mindepth 1 -delete
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1
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
    && mkdir -p /app /data /cache \
    && chown botarena:botarena /data /cache
WORKDIR /app
COPY --from=app-build /app/publish /app/publish
COPY --from=app-build /app/maps /app/maps
COPY --from=app-build /app/champions /app/champions
COPY --from=app-build /app/artifacts/wasm /app/artifacts/wasm
# The frozen minds the Arc Relay seeder loads. This image runs BOTARENA_ROLE=migrate
# in production, and that role seeds the immutable playlist, so without these the
# migration aborts on "Frozen Arc Relay stock mind artifact is missing".
COPY --from=arena-artifacts /prune/arena-bots /app/arena-bots
COPY --from=web /src/web/dist /app/web/dist
ENV BOTARENA_ROOT=/app \
    BOTARENA_DATA=/data \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_EnableDiagnostics=0
VOLUME /data /cache
EXPOSE 8080
USER botarena
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1
CMD ["dotnet", "/app/publish/BotArena.App.dll"]
