using System.IO;
using System.IO.Compression;
using System.Text;
using WhatAmIDoing;

namespace WhatAmIDoing.Services;

/// <summary>
/// Manual “two PC” handoff: copies the SQLite database into a zip with instructions (no merge logic).
/// </summary>
public static class TwoPcHandoffService
{
    public static void WriteHandoffZip(string zipPath)
    {
        var dbSource = AppPaths.DatabasePath;
        if (!File.Exists(dbSource))
            throw new InvalidOperationException("Database file not found.");

        var dir = Path.Combine(Path.GetTempPath(), "WhatAmIDoing-handoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var destDb = Path.Combine(dir, "activity.sqlite3");
            File.Copy(dbSource, destDb, overwrite: true);

            File.WriteAllText(Path.Combine(dir, "README-handoff.txt"),
                """
                What Am I Doing — database handoff
                ---------------------------------
                This zip contains a copy of activity.sqlite3 from this PC.

                On another PC with What Am I Doing installed:
                1. Quit the app completely (tray Exit).
                2. Replace %LocalAppData%\WhatAmIDoing\activity.sqlite3 with this file (backup the old file first), OR
                   use Settings → Import database backup… and choose this file inside an extracted folder.

                Import replaces ALL data on that PC with this database. Use only files you trust.

                There is no automatic merge of two databases in v1 — pick one file as canonical or export/import selectively via backup files.
                """.Trim(), Encoding.UTF8);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(dir, zipPath);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }
    }
}
