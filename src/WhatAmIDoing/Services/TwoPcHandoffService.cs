using System.IO;
using System.IO.Compression;
using System.Text;
using WhatAmIDoing;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Manual “two PC” handoff: copies the SQLite database into a zip with instructions (no merge logic).
/// </summary>
public static class TwoPcHandoffService
{
    public static void WriteHandoffZip(string zipPath, AppDatabase db)
    {
        var dbSource = AppPaths.DatabasePath;
        if (!File.Exists(dbSource))
            throw new InvalidOperationException("Database file not found.");

        UserTempHygiene.WithStagingDirectory("handoff", dir =>
        {
            var destDb = Path.Combine(dir, "activity.sqlite3");
            if (File.Exists(destDb) && !UserTempHygiene.TryDeleteFile(destDb))
                throw new IOException("Could not clear the previous handoff file in Temp.");
            db.BackupDatabaseToFile(destDb);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(destDb, "activity.sqlite3", CompressionLevel.Optimal);
            WriteEntry(zip, "README-handoff.txt",
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
                """.Trim());
        });
    }

    private static void WriteEntry(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(text);
    }
}
