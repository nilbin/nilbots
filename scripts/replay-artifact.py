#!/usr/bin/env python3
"""Strip a nilbots viewer.html down to embed-ready page content.

Artifact hosts wrap published files in their own <head>/<body> skeleton, so a
full HTML document nests badly. viewer.html keeps every asset (styles, scripts,
embedded replay) inline, so head + body content concatenated in order is a
faithful standalone fragment: the replay-injection script still precedes the
deferred module script, which finds #root after parse.

Usage: replay-artifact.py <viewer.html> <out.html> [title]
"""
import re
import sys


def main() -> None:
    if len(sys.argv) < 3:
        sys.exit(__doc__)
    src, dst = sys.argv[1], sys.argv[2]
    html = open(src, encoding="utf-8").read()
    head_start, head_end = html.find("<head>"), html.find("</head>")
    body_start, body_end = html.find("<body>"), html.rfind("</body>")
    if min(head_start, head_end, body_start, body_end) < 0:
        sys.exit(f"{src}: not a full HTML document (missing head/body tags).")
    head = html[head_start + 6 : head_end]
    body = html[body_start + 6 : body_end]
    if len(sys.argv) > 3:
        head = re.sub(r"<title>.*?</title>", f"<title>{sys.argv[3]}</title>", head, flags=re.S)
    with open(dst, "w", encoding="utf-8") as out:
        out.write(head.strip() + "\n" + body.strip() + "\n")
    print(f"{dst}: embed-ready ({head_end - head_start + body_end - body_start} bytes)")


if __name__ == "__main__":
    main()
