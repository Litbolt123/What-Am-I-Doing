# What Am I Doing 1.1.4 — Windows installer

## What's new

- **Reliable “newer on GitHub” detection:** The app no longer uses GitHub’s single **`/releases/latest`** pointer alone. It scans **published releases** (paginated API), skips **drafts**, and compares your install to the **highest** release tag it can parse — so you still see **1.1.3** (or any newer tag) even when “Latest” on GitHub pointed at an older build (for example two installers published close together, or a pre-release). Draft releases are ignored; tags must look like normal `Major.Minor.Patch` versions for parsing.
- **Updates live in Settings:** **Check for updates…**, status text, **Open Releases page**, and **Download and run installer…** are under **Settings → Updates (GitHub)** next to the auto-check and tray-notification toggles. **About** stays for version and data path only, with a short note to use Settings for GitHub checks.
- **Docs:** [`docs/updating-app.md`](docs/updating-app.md) and the updater test note describe the new behavior; automatic check copy mentions that GitHub may receive more than one lightweight request if the repo has many pages of old releases.

## Notes

- After installing **1.1.4** on a PC that was stuck showing “up to date” on **1.1.2**, use **Check for updates…** again (or wait for the post-start check) to see any newer published tag, including **1.1.3** or later.
