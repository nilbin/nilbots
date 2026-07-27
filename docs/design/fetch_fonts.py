#!/usr/bin/env python3
"""Pull the latin cut of each face and inline it, since the artifact CSP blocks CDNs."""
import base64
import json
import re
import subprocess
import sys
from pathlib import Path

UA = ("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/120.0 Safari/537.36")
here = Path(__file__).parent
out = here / "fonts"
out.mkdir(exist_ok=True)


def get(url: str) -> bytes:
    r = subprocess.run(["curl", "-sS", "-m", "25", "-A", UA, url], capture_output=True)
    if r.returncode:
        raise SystemExit(f"fetch failed: {url}\n{r.stderr.decode()}")
    return r.stdout


def latin_src(query: str) -> str:
    css = get(f"https://fonts.googleapis.com/css2?family={query}&display=swap").decode()
    blocks = css.split("@font-face")
    for b in blocks:
        if "U+0000-00FF" in b and "0100-02BA" not in b.split("unicode-range")[-1][:40]:
            m = re.search(r"src: url\((\S+?)\) format", b)
            if m:
                return m.group(1)
    raise SystemExit(f"no latin block for {query}\n{css[:400]}")


FACES = {
    "archivo": "Archivo:wdth,wght@75..125,400..700",
    "mono": sys.argv[1] if len(sys.argv) > 1 else "Geist+Mono:wght@400..500",
}

manifest = {}
for name, query in FACES.items():
    url = latin_src(query)
    data = get(url)
    (out / f"{name}.woff2").write_bytes(data)
    manifest[name] = {"query": query, "url": url, "bytes": len(data),
                      "b64": base64.b64encode(data).decode()}
    print(f"{name:9} {len(data):>7,} B  ->  {len(manifest[name]['b64']):>7,} B base64")

(out / "manifest.json").write_text(json.dumps(manifest))
