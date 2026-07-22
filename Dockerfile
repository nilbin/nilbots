# Bot Arena pilot image: the modular monolith plus the full bot toolchain.
# The server compiles player submissions to WASM at runtime, so the image
# carries the .NET SDK, clang/wasm32 libs and the synthetic wasi-sdk.

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

FROM ubuntu:24.04
RUN apt-get update && apt-get install -y --no-install-recommends \
      curl ca-certificates clang-18 lld-18 wasi-libc \
      libclang-rt-18-dev-wasm32 libc++-18-dev-wasm32 libc++abi-18-dev-wasm32 \
    && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

WORKDIR /app
COPY . .
RUN rm -rf web && bash scripts/setup-wasi-sdk.sh
RUN dotnet build BotArena.sln -c Release -v q
RUN bash scripts/build-wasm-guest.sh
# Prime the submission toolchain: one throwaway bot build pre-downloads the
# NativeAOT-LLVM packages so the first player submission doesn't pay for it.
RUN cd /tmp && dotnet run --project /app/src/BotArena.Cli -c Release -- new PrimeBot \
    && BOTARENA_ROOT=/app dotnet run --project /app/src/BotArena.Cli -c Release -- build /tmp/PrimeBot \
    && rm -rf /tmp/PrimeBot
COPY --from=web /src/web/dist /app/web/dist

ENV BOTARENA_ROOT=/app \
    BOTARENA_DATA=/data \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
VOLUME /data
EXPOSE 8080
CMD ["dotnet", "run", "--project", "src/BotArena.App", "-c", "Release", "--no-build"]
