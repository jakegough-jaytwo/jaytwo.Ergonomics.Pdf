"""Fetch the pinned EU DSS bundle with bounded, parallel HTTP range requests."""
import concurrent.futures
import hashlib
import pathlib
import shutil
import sys
import time
import urllib.request

URL = "https://ec.europa.eu/digital-building-blocks/artifact/repository/esignaturedss/eu/europa/ec/joinup/sd-dss/dss-demo-bundle/6.3/dss-demo-bundle-6.3.zip"
SIZE = 370217442
SHA256 = "60452c53eb395106655340890a207885f165bf844485046e3a1f8535ef8fa944"
PARTS = 16
target = pathlib.Path(sys.argv[1])
parts = [target.with_suffix(f".part{index}") for index in range(PARTS)]


def download(index):
    start, end = SIZE * index // PARTS, SIZE * (index + 1) // PARTS - 1
    request = urllib.request.Request(URL, headers={"Range": f"bytes={start}-{end}"})
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                if response.status != 206 or response.headers.get("Content-Range") != f"bytes {start}-{end}/{SIZE}":
                    raise RuntimeError("DSS server did not honor the requested byte range")
                with parts[index].open("wb") as output:
                    shutil.copyfileobj(response, output, length=65536)
            if parts[index].stat().st_size != end - start + 1:
                raise RuntimeError("Incomplete DSS bundle range")
            print(f"DSS bundle range {index + 1}/{PARTS} complete", flush=True)
            return
        except Exception:
            if attempt == 2:
                raise
            time.sleep(1)


try:
    with concurrent.futures.ThreadPoolExecutor(max_workers=PARTS) as pool:
        list(pool.map(download, range(PARTS)))
    with target.open("wb") as output:
        for part in parts:
            with part.open("rb") as source:
                shutil.copyfileobj(source, output, length=65536)
    with target.open("rb") as source:
        actual = hashlib.file_digest(source, "sha256").hexdigest()
    if actual != SHA256:
        target.unlink()
        raise RuntimeError(f"DSS bundle SHA-256 mismatch: {actual}")
    print(f"DSS 6.3 bundle SHA-256 verified: {actual}", flush=True)
finally:
    for part in parts:
        part.unlink(missing_ok=True)
