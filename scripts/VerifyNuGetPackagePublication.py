#!/usr/bin/env python3

import os
import time
import urllib.error
import urllib.request


RIDS = (
    "win-x64",
    "win-arm64",
    "linux-x64",
    "linux-arm64",
    "linux-musl-x64",
    "linux-musl-arm64",
    "osx-x64",
    "osx-arm64",
)


def main() -> None:
    version = os.environ.get("PACKAGE_VERSION", "").strip().lower()
    if not version:
        raise SystemExit("PACKAGE_VERSION is required.")

    package_ids = {f"Techne.Loom.Runtime.{rid}" for rid in RIDS}
    package_ids.add("Techne.Loom.Abstractions")
    pending = set(package_ids)
    for attempt in range(120):
        for package_id in tuple(pending):
            package_lower = package_id.lower()
            url = f"https://api.nuget.org/v3-flatcontainer/{package_lower}/{version}/{package_lower}.{version}.nupkg"
            try:
                request = urllib.request.Request(url, method="HEAD")
                with urllib.request.urlopen(request, timeout=20) as response:
                    if response.status == 200:
                        pending.remove(package_id)
            except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError):
                pass
        if not pending:
            print(f"NuGet.org serves all 9 package IDs at exact version {version}.")
            return
        if attempt < 119:
            print(f"Waiting for NuGet.org validation ({attempt + 1}/120): {sorted(pending)}", flush=True)
            time.sleep(30)
    raise SystemExit(f"NuGet.org did not expose exact version {version} for: {sorted(pending)}")


if __name__ == "__main__":
    main()
