#!/usr/bin/env bash
# Builds a synthetic wasi-sdk-29.0 at ~/.wasi-sdk/wasi-sdk-29.0 from Ubuntu packages.
#
# Why: the official wasi-sdk ships via GitHub releases, which some sandboxed
# environments (like Claude Code remote sessions) cannot download. Ubuntu 24.04
# packages clang-18/lld plus wasm32 builds of wasi-libc, compiler-rt and libc++,
# which are enough to link NativeAOT-LLVM output into a WASI p1 core module.
# On an unrestricted machine, prefer the real wasi-sdk-29 and skip this script.
#
# The Ubuntu wasi-libc (2023 snapshot) predates single-threaded pthread stubs,
# so a tiny stub archive supplies those symbols (see botarena_stubs.c below).
set -euo pipefail

# World-readable so unprivileged submission builds (BuildIsolation) can use it;
# ~/.wasi-sdk keeps a compatibility symlink.
SDK="${BOTARENA_WASI_SDK:-/opt/botarena/wasi-sdk-29.0}"
if [ -x "$SDK/bin/clang" ] && [ -f "$SDK/stubs/libbotarena_stubs.a" ]; then
  echo "Synthetic wasi-sdk already present at $SDK"
  exit 0
fi

if dpkg -s clang-18 lld-18 wasi-libc libclang-rt-18-dev-wasm32 \
    libc++-18-dev-wasm32 libc++abi-18-dev-wasm32 >/dev/null 2>&1; then
  echo "Ubuntu wasm32 toolchain packages already installed."
else
  echo "Installing Ubuntu wasm32 toolchain packages..."
  apt-get update >/dev/null
  apt-get install -y clang-18 lld-18 wasi-libc \
    libclang-rt-18-dev-wasm32 libc++-18-dev-wasm32 libc++abi-18-dev-wasm32 >/dev/null
fi

mkdir -p "$SDK/bin" "$SDK/share/wasi-sysroot/include" "$SDK/share/wasi-sysroot/lib" "$SDK/stubs"
echo "29.0" > "$SDK/VERSION"

# clang driver looks for compiler-rt builtins under its resource dir per target OS.
RT=$(dpkg -L libclang-rt-18-dev-wasm32 | grep "libclang_rt.builtins-wasm32.a$" | head -1)
for d in wasip2 wasip1 wasi; do
  mkdir -p "/usr/lib/llvm-18/lib/clang/18/lib/$d"
  TARGET="/usr/lib/llvm-18/lib/clang/18/lib/$d/libclang_rt.builtins-wasm32.a"
  [ "$RT" = "$TARGET" ] || ln -sf "$RT" "$TARGET"
done

# Sysroot: Ubuntu wasi-libc + wasm32 libc++ under every triple spelling clang may use.
CXXDIR=$(dirname "$(dpkg -L libc++-18-dev-wasm32 | grep "libc++.a$" | head -1)")
for t in wasm32-wasi wasm32-wasip1 wasm32-wasip2 wasm32-unknown-wasip2; do
  mkdir -p "$SDK/share/wasi-sysroot/lib/$t"
  for f in /usr/lib/wasm32-wasi/* "$CXXDIR"/*.a; do ln -sf "$f" "$SDK/share/wasi-sysroot/lib/$t/"; done
  ln -sfn /usr/include/wasm32-wasi "$SDK/share/wasi-sysroot/include/$t" 2>/dev/null || true
done

cat > "$SDK/bin/clang" <<'WRAP'
#!/bin/bash
SDKDIR="$(cd "$(dirname "$0")/.." && pwd)"
extra=("-B" "$SDKDIR/bin")
case " $* " in *"-target"*|*"--target"*) ;; *) extra+=("--target=wasm32-wasi");; esac
case " $* " in *"--sysroot"*) ;; *) extra+=("--sysroot=$SDKDIR/share/wasi-sysroot");; esac
exec /usr/bin/clang-18 "${extra[@]}" "$@"
WRAP
chmod +x "$SDK/bin/clang"
ln -sf "$SDK/bin/clang" "$SDK/bin/clang++"
for tool in wasm-ld llvm-ar llvm-nm llvm-ranlib llvm-objcopy llvm-strip; do
  [ -x "/usr/bin/$tool-18" ] && ln -sf "/usr/bin/$tool-18" "$SDK/bin/$tool"
done

# NativeAOT-LLVM (net10) targets wasm32-unknown-wasip2, whose clang driver links via
# wasm-component-ld. We want a plain p1 core module, so this shim strips the
# component-specific flags and calls wasm-ld, appending our libc stubs.
cat > "$SDK/bin/wasm-component-ld" <<'WRAP'
#!/bin/bash
args=(); skip=0
for a in "$@"; do
  if [ $skip -eq 1 ]; then skip=0; continue; fi
  case "$a" in
    --component-type|--wasm-ld-path|--adapter) skip=1 ;;
    --component-type=*|--wasm-ld-path=*|--adapter=*) ;;
    */bin/wasm-ld) ;;
    *) args+=("$a") ;;
  esac
done
exec /usr/bin/wasm-ld-18 "${args[@]}" -lbotarena_stubs
WRAP
chmod +x "$SDK/bin/wasm-component-ld"

cat > "$SDK/stubs/botarena_stubs.c" <<'STUBS'
/* Single-threaded pthread stubs for the pre-thread-stub Ubuntu wasi-libc,
   plus C++ EH symbols that must never actually be reached. */
#include <stdlib.h>
int pthread_mutex_init(void *m, const void *a) { return 0; }
int pthread_mutex_destroy(void *m) { return 0; }
int pthread_mutex_lock(void *m) { return 0; }
int pthread_mutex_unlock(void *m) { return 0; }
int pthread_mutexattr_init(void *a) { return 0; }
int pthread_mutexattr_destroy(void *a) { return 0; }
int pthread_mutexattr_settype(void *a, int t) { return 0; }
int pthread_cond_init(void *c, const void *a) { return 0; }
int pthread_cond_destroy(void *c) { return 0; }
int pthread_cond_wait(void *c, void *m) { return 0; }
int pthread_cond_timedwait(void *c, void *m, const void *t) { return 0; }
int pthread_cond_broadcast(void *c) { return 0; }
int pthread_condattr_init(void *a) { return 0; }
unsigned long pthread_self(void) { return 1; }
const char *gai_strerror(int e) { return "gai error"; }
void *__cxa_allocate_exception(unsigned long size) { abort(); }
void __cxa_throw(void *thrown, void *type, void *dtor) { abort(); }
STUBS
"$SDK/bin/clang" -O2 -c "$SDK/stubs/botarena_stubs.c" -o "$SDK/stubs/botarena_stubs.o"
llvm-ar-18 rcs "$SDK/stubs/libbotarena_stubs.a" "$SDK/stubs/botarena_stubs.o"
for t in wasm32-wasi wasm32-wasip1 wasm32-wasip2 wasm32-unknown-wasip2; do
  ln -sf "$SDK/stubs/libbotarena_stubs.a" "$SDK/share/wasi-sysroot/lib/$t/"
done

mkdir -p "$HOME/.wasi-sdk"
ln -sfn "$SDK" "$HOME/.wasi-sdk/wasi-sdk-29.0"
chmod -R a+rX "$SDK"

echo "Synthetic wasi-sdk ready at $SDK"
