#!/usr/bin/env python3
"""Builds the CrazyGames upload: one self-contained index.html (fonts embedded, CrazyGames SDK on,
online server URL baked in) zipped as dist/sumo-noodles-crazygames.zip.

    python3 build/build_crazygames.py --server wss://YOUR-APP.fly.dev
"""
import argparse, base64, os, re, zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ap = argparse.ArgumentParser()
ap.add_argument("--server", default="wss://sumo-noodles.fly.dev", help="WebSocket URL of the fly.io server")
ap.add_argument("--no-sdk", action="store_true", help="leave the CrazyGames SDK out (for testing elsewhere)")
args = ap.parse_args()

html = open(os.path.join(ROOT, "index.html"), encoding="utf-8").read()

def b64(name):
    return base64.b64encode(open(os.path.join(HERE, "fonts", name), "rb").read()).decode()

fonts = (
    "<style>\n"
    f"@font-face{{font-family:'Baloo 2';font-style:normal;font-weight:600 800;font-display:swap;src:url(data:font/woff2;base64,{b64('Baloo2-latin.woff2')}) format('woff2');}}\n"
    f"@font-face{{font-family:'Lilita One';font-style:normal;font-weight:400;font-display:swap;src:url(data:font/woff2;base64,{b64('LilitaOne-latin.woff2')}) format('woff2');}}\n"
    "</style>"
)
# Google Fonts links -> embedded fonts (no third-party requests)
html, n = re.subn(r'<link rel="preconnect" href="https://fonts.googleapis.com">\s*<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\s*<link rel="stylesheet" href="https://fonts.googleapis.com/[^"]*">', lambda m: fonts, html)
assert n == 1, "font links not found"
# CrazyGames SDK + server URL
head = f'<script>window.SUMO_SERVER = {args.server!r};</script>'
if not args.no_sdk:
    head = '<script src="https://sdk.crazygames.com/crazygames-sdk-v3.js"></script>\n' + head
html, n = re.subn(r"<!-- CrazyGames build:.*?-->", lambda m: head, html)
assert n == 1, "SDK marker not found"
if not html.lstrip().lower().startswith("<!doctype"):
    html = ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n'
            '<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n') + html.replace("</style>\n\n<canvas", "</style>\n</head>\n<body>\n<canvas", 1) + "\n</body>\n</html>\n"

out_dir = os.path.join(ROOT, "dist", "crazygames")
os.makedirs(out_dir, exist_ok=True)
page = os.path.join(out_dir, "index.html")
open(page, "w", encoding="utf-8").write(html)
zpath = os.path.join(ROOT, "dist", "sumo-noodles-crazygames.zip")
with zipfile.ZipFile(zpath, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(page, "index.html")
print(f"built {page} ({os.path.getsize(page)//1024} KB) and {zpath} ({os.path.getsize(zpath)//1024} KB)")
