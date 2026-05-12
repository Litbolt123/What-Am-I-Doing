# Testing the in-app updater (two releases)

The repo is bumped twice on `main`: **1.1.2** (real small release) then **1.1.3** (version-only bump to exercise “newer on GitHub”).

## 1. Push `main`, then tag both releases

After your two version commits are on GitHub:

```powershell
git checkout main
git pull
```

Tag **v1.1.2** on the commit that still has `<Version>1.1.2</Version>` (the parent of the 1.1.3 bump if both commits are linear):

```powershell
git log --oneline -3
# Find the commit hash for "Release 1.1.2" (or equivalent message)

git tag v1.1.2 <that-commit-sha>
git tag v1.1.3 HEAD
git push origin main
git push origin v1.1.2 v1.1.3
```

Wait for both **Windows installer** workflow runs to finish and publish two GitHub Releases.

## 2. Install 1.1.2 on a test PC

On the **v1.1.2** release page, download **`WhatAmIDoing-Setup-1.1.2.exe`** (not necessarily “Latest”, which may be 1.1.3). Install and run once so startup update check is enabled.

## 3. See 1.1.3 as newer

With **1.1.3** published, within ~12 seconds (or after **Settings → Updates (GitHub) → Check for updates…**), the app should report a newer release and can show the tray balloon / **Download and run installer…** when the setup exe is attached to the release.

## 4. Optional cleanup

After testing, delete the **v1.1.3** release on GitHub if you do not want a permanent “test” version listed, or leave it and ship **1.1.4** next from `main`.

## Same idea with 1.1.4 → 1.1.5

After the **paginated release-scan** fix shipped in **1.1.4**, you can repeat the test: install **`WhatAmIDoing-Setup-1.1.4.exe`**, then tag and publish **1.1.5** from `main` and confirm **Settings → Updates (GitHub) → Check for updates…** reports **1.1.5** as newer (or use the tray balloon when enabled).
