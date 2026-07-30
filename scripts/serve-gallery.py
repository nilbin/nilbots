#!/usr/bin/env python3
"""Serve a review gallery, sending the pre-compressed copy when there is one.

`python3 -m http.server` ships every byte uncompressed. For a replay gallery
watched on a phone over mobile data that is the whole cost: replay JSON
compresses ~10x, so the difference is a 1-2 MB download or a 15-20 MB one.

This is that same stdlib server plus one rule: when the client accepts gzip
and a `<file>.gz` sibling exists on disk, serve the sibling with
`Content-Encoding: gzip` and the *original's* content type.
`scripts/build-review-gallery.py` writes those siblings, so nothing is
compressed per request — the server cannot get compression wrong, only skip
it, and a client that does not accept gzip transparently gets the plain file.

    python3 scripts/serve-gallery.py 8931 --directory sandbox/review-gallery

Serve the gallery directory AS THE SERVER ROOT: hosted gallery pages
reference `/assets` and `/soundtracks` by absolute path.
"""

from __future__ import annotations

import argparse
import functools
import os
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer


class GalleryHandler(SimpleHTTPRequestHandler):
    # Keep-alive matters here: one page pulls the bundle, the atlases and the
    # soundtrack catalog. Content-Length is set on every response the base
    # class produces, which is what HTTP/1.1 requires of us.
    protocol_version = "HTTP/1.1"
    _gzipped = False

    def handle_one_request(self) -> None:
        self._gzipped = False
        super().handle_one_request()

    def translate_path(self, path: str) -> str:
        local = super().translate_path(path)
        # `/` is the gallery index, and the base class resolves the directory
        # index after this hook — so resolve it here too, or the one page
        # every visit starts on is the one page served uncompressed.
        candidate = local
        if path.split("?", 1)[0].endswith("/") and os.path.isdir(local):
            candidate = os.path.join(local, "index.html")
        if (not candidate.endswith(".gz")
                and "gzip" in self.headers.get("Accept-Encoding", "")
                and os.path.isfile(candidate + ".gz")):
            self._gzipped = True
            return candidate + ".gz"
        self._gzipped = False
        return local

    def guess_type(self, path):
        # The response describes the file the client asked for; only the
        # transfer encoding differs.
        if self._gzipped and str(path).endswith(".gz"):
            path = str(path)[: -len(".gz")]
        return super().guess_type(path)

    def end_headers(self) -> None:
        self.send_header("Vary", "Accept-Encoding")
        if self._gzipped:
            self.send_header("Content-Encoding", "gzip")
        # Hashed bundle output is immutable by construction; everything else
        # keeps the base class's Last-Modified revalidation.
        if self.path.startswith("/assets/"):
            self.send_header("Cache-Control", "public, max-age=31536000, immutable")
        super().end_headers()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("port", nargs="?", type=int, default=8000)
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--directory", default=os.getcwd())
    args = parser.parse_args()
    handler = functools.partial(GalleryHandler, directory=args.directory)
    with ThreadingHTTPServer((args.bind, args.port), handler) as server:
        print(f"serving {args.directory} on {args.bind}:{args.port} "
              "(gzip siblings enabled)", flush=True)
        server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
