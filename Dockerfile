# nilbots images. `runtime` serves web/match/migrate/coordinator roles without
# a compiler; `compiler` adds the pinned C#→WASM toolchain and runs entirely as
# the unprivileged botbuild account.

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
COPY --from=web /src/web/dist /app/web/dist

FROM toolchain AS compiler
RUN mkdir -p /compiler-ipc /work \
    && chown botbuild:botbuild /compiler-ipc /work
ENV BOTARENA_ROOT=/app \
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
