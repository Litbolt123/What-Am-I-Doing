# Publishing a Windows release (What Am I Doing)

This guide is for **you as the maintainer**: how to ship a new **Windows installer** and, if you want, how it shows up on **GitHub Releases**. It matches how **this** repo is wired (`Directory.Build.props`, tag `v*`, and [`.github/workflows/release-windows.yml`](../.github/workflows/release-windows.yml)).

---

## 1. Words that sound the same but are not

| Name | What it is | Example |
|------|----------------|----------|
| **`<Version>` in `Directory.Build.props`** | The **numeric** app + installer version MSBuild and Inno use. **Single source of truth.** | `1.0.0` |
| **Git tag** | A label on a **specific commit** in Git. Our release workflow only treats tags that look like **`v` + numbers**. | `v1.0.0` |
| **GitHub Release** | A **page on GitHub** (title, notes, attached files) under **Releases**. Often created by CI when you push a tag. | Release titled `v1.0.0` with `WhatAmIDoing-Setup-1.0.0.exe` |
| **Release title** | The **headline** GitHub shows for that release. Our workflow defaults to the **tag name**; you can edit it later. | `v1.0.0` or `What Am I Doing 1.0.0` |
| **Artifact** | A **zip / file** attached to an **Actions** workflow run. **Not** the same as Releases until you publish a Release. | `WhatAmIDoing-Setup-1.0.0` download on a run |

**Rule you cannot break in this repo:**  
If `<Version>` is `1.0.2`, the tag **must** be **`v1.0.2`** (letter `v`, then **exactly** the same numbers as `<Version>`).  
If you push `v1.0.3` while `<Version>` is still `1.0.2`, the **Verify tag matches Version** step fails on purpose.

---

## 2. Where the version lives (only bump here for releases)

At the **repo root**, edit **`Directory.Build.props`**:

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

- **`Version`** — what users see in “About”, what Inno uses (`AppVersion`), and what the Git tag must match (without the `v`).
- **`AssemblyVersion` / `FileVersion`** — usually keep the **same numbers** as `Version` (often `1.0.0.0` for a `1.0.0` product version).

**Commit and push** these changes to GitHub **before** you tag, if the new release needs a new version number.

**Sanity check locally** (prints the same value CI uses):

```powershell
powershell -NoProfile -File .\scripts\get-version.ps1
```

---

## 3. How naming works (tags, releases, filenames)

### Git tags (required for automated Releases)

- Use **semantic versioning** style: **`vMAJOR.MINOR.PATCH`**
  - **PATCH** (`1.0.0` → `1.0.1`): bugfixes, tiny tweaks, “rebuild same product line.”
  - **MINOR** (`1.0.x` → `1.1.0`): new features, still compatible for users.
  - **MAJOR** (`1.x` → `2.0.0`): breaking changes or “big new era.”
- Tag format here: **`v` + `<Version>`** only, e.g. `1.0.0` → **`v1.0.0`**.

### GitHub Release title

- CI can create the release with **`generate_release_notes: true`** — GitHub fills in a simple changelog from commits.
- You may **rename** the release title after the fact (e.g. “1.0.0 — first public build”) if you like; it does not have to equal the tag, but keeping them aligned reduces confusion.

### Installer filename

- Inno outputs something like **`WhatAmIDoing-Setup-1.0.0.exe`** — driven by **`AppVersion`** from MSBuild, i.e. **`Directory.Build.props`**.

---

## 4. Two ways CI builds the installer (only one fills Releases)

| Trigger | What happens | **Releases** page |
|--------|----------------|-------------------|
| **Actions → Windows installer → Run workflow** (manual) on `main` | Builds installer, uploads **artifact** | **Still empty** (no automated Release) |
| **`git push origin v1.0.0`** (tag push) | Same build **+** **Publish GitHub Release** step runs | **Gets** a new release (if the job is green) |

So: **manual run = good for testing CI.** **Tag push = “this is a version I ship to Releases.”**

---

## 5. Recommended path: ship a version to Releases (checklist)

Assume you want to ship **`1.0.0`** and the repo already has `<Version>1.0.0</Version>` on `main`.

1. **Sync your machine with GitHub**
   ```powershell
   git checkout main
   git pull
   ```

2. **Confirm version** (optional but reassuring)
   ```powershell
   powershell -NoProfile -File .\scripts\get-version.ps1
   ```
   It should print `1.0.0`.

3. **Create the tag on the commit you are shipping**  
   (Usually the latest `main` after your last push.)
   ```powershell
   git tag v1.0.0
   ```

4. **Push only the tag** (this starts the release workflow on GitHub)
   ```powershell
   git push origin v1.0.0
   ```

5. **On GitHub → Actions → “Windows installer”**  
   - Open the run that shows **`v1.0.0`** (not only `main`).  
   - Wait until it is **green**.

6. **On GitHub → Releases**  
   - You should see **`v1.0.0`** (or similar) with **`WhatAmIDoing-Setup-1.0.0.exe`** attached.

**Private repo:** Releases work the same; only people with access to the repo see them.

---

## 6. If something goes wrong

### “Verify tag matches Version” failed

- The tag **without** `v` must equal `<Version>`.  
  Example: tag `v1.0.1` requires `<Version>1.0.1</Version>`.

**Fix:** Either change `Directory.Build.props` and push `main`, then tag again — or delete the wrong tag and tag the right commit:

```powershell
git push origin :refs/tags/v1.0.1
# fix Version on main, commit, push, then:
git tag v1.0.1
git push origin v1.0.1
```

### Tag already exists locally / on GitHub

Delete locally and on remote, then recreate (only if you are sure no one depends on that tag):

```powershell
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
git tag v1.0.0
git push origin v1.0.0
```

### Build failed for another reason (Inno, publish, fetch)

- Open the failed **step** in the Actions log; the last lines usually name the problem.
- Fix on `main`, **push**, then either:
  - **Move** the tag to the new commit (advanced), or  
  - **Bump** `<Version>` (e.g. `1.0.1`) and push a **new** tag `v1.0.1`.

### Releases is still empty but the tag run is green

- Open that run and check the **“Publish GitHub Release”** step: it only runs for **`refs/tags/v*`**.
- If it failed, expand it — permissions or `softprops/action-gh-release` errors are rare but possible.

---

## 7. “I only want the installer file, not Releases”

1. **Actions → Windows installer → Run workflow** on `main`.  
2. Open the **green** run → **Artifacts** → download **`WhatAmIDoing-Setup-…`**.

That is enough for testers or for attaching to a **manually** drafted release later.

---

## 8. Local build (without GitHub)

Same version rules: **`Directory.Build.props`** drives Inno.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Output: **`installer\Output\WhatAmIDoing-Setup-<Version>.exe`**.

---

## 9. Quick reference

| I want to… | Do this |
|------------|---------|
| Test CI without Releases | **Actions → Run workflow** on `main` |
| Publish to **Releases** | **`git tag vX.Y.Z`** then **`git push origin vX.Y.Z`** with `<Version>` = `X.Y.Z` |
| Bump version for next ship | Edit **`Directory.Build.props`**, commit, push, then new **`v…`** tag |
| See current version from disk | **`.\scripts\get-version.ps1`** |

---

## 10. Files involved (for your future self)

| File | Role |
|------|------|
| [`Directory.Build.props`](../Directory.Build.props) | `<Version>` — must match tag `v<Version>` |
| [`.github/workflows/release-windows.yml`](../.github/workflows/release-windows.yml) | CI: tag `v*` → build + Release; manual → artifact only |
| [`installer/WhatAmIDoing.iss`](../installer/WhatAmIDoing.iss) | Inno wizard; `AppVersion` passed in by CI |
| [`scripts/build-installer.ps1`](../scripts/build-installer.ps1) | Local one-shot: fetch runtime + publish + Inno |
| [`scripts/get-version.ps1`](../scripts/get-version.ps1) | Print MSBuild `Version` |

You are not expected to memorize all of this — **tag = `v` + `Directory.Build.props` Version**, **push tag**, **wait for green Actions on that tag**, **open Releases**.
