# syntax=docker/dockerfile:1
#
# Cross-platform C# -> WASM build environment.
#
# NativeAOT-LLVM's pinned compiler host is Linux x64 only. Linux x64 developers
# use it natively; macOS/Apple Silicon and Linux arm64 run this same environment
# through Docker's linux/amd64 emulation. Keep this image focused: it contains
# the compiler prerequisites, not the app, web viewer, database, or source tree.
FROM ubuntu:24.04

ARG DOTNET_CHANNEL=10.0

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      ca-certificates curl clang-18 llvm-18 lld-18 wasi-libc \
      libclang-rt-18-dev-wasm32 libc++-18-dev-wasm32 libc++abi-18-dev-wasm32 \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel "${DOTNET_CHANNEL}" --install-dir /opt/dotnet \
    && ln -s /opt/dotnet/dotnet /usr/local/bin/dotnet \
    && rm /tmp/dotnet-install.sh

COPY scripts/setup-wasi-sdk.sh /tmp/setup-wasi-sdk.sh
RUN BOTARENA_WASI_SDK=/opt/botarena/wasi-sdk-29.0 bash /tmp/setup-wasi-sdk.sh \
    && rm /tmp/setup-wasi-sdk.sh \
    && mkdir -p /workspace /cache/dotnet /cache/nuget \
    && chmod -R 0777 /workspace /cache

ENV WASI_SDK_PATH=/opt/botarena/wasi-sdk-29.0 \
    DOTNET_CLI_HOME=/cache/dotnet \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_PACKAGES=/cache/nuget

WORKDIR /workspace
