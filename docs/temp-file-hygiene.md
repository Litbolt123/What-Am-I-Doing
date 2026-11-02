# Temp files

Standing rule (August, 2026-11-01), every Helios app: do not leave growing `%TEMP%` copies of large databases or caches. Prefer a live read-only read. When a copy is required, use one fixed overwrite path per process, delete it with retries, and sweep leftovers on startup.

What Am I Doing keeps user exports (support bundle, PC handoff, database backup). Nothing on a timer or update poll copies `activity.sqlite3` into Temp.

- **Support bundle** writes the zip straight to the folder the user picked. It does not create a Temp directory.
- **PC handoff** snapshots the open database with `VACUUM INTO` to `%TEMP%\WhatAmIDoing\handoff\activity.sqlite3` (one path, overwritten), zips that file to the chosen location, then deletes the staging directory on success and on failure.
- **Installer download** writes `%TEMP%\WhatAmIDoing\setup\WhatAmIDoing-Setup.exe` only. If that file stays locked, the download stops with an error. It does not create a second Guid-named exe. A failed download deletes the partial file. A successful download leaves that one file so setup can run; the next launch removes it if it is no longer in use.
- **Startup** sweeps old `WhatAmIDoing-support-*` and `WhatAmIDoing-handoff-*` directories, `WhatAmIDoing-Setup*.exe` files in Temp, and `%TEMP%\WhatAmIDoing`.
