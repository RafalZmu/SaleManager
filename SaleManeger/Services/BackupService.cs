using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SaleManeger.Services
{
    public record BackupInfo(string Path, DateTime Time, long Size, bool IsSafetyCopy)
    {
        public string FileName => System.IO.Path.GetFileName(Path);
        public string TimeText => Time.ToString("dd.MM.yyyy HH:mm:ss");
        public string SizeText => $"{Size / 1024.0:0} KB";
        public string Kind => IsSafetyCopy ? "przed przywróceniem" : "kopia";
    }

    /// <summary>
    /// Local copies of sale.db. They are taken through the SQLite backup API, so a copy is consistent even
    /// while the app has the database open, and old ones are removed so the folder does not grow forever.
    /// </summary>
    public static class BackupService
    {
        private const string Prefix = "sale-";
        private const string SafetyPrefix = "sale-przed-przywroceniem-";
        private const string TimeFormat = "yyyyMMdd-HHmmss";

        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaleManagerBackups");

        /// <summary>Takes a copy at app start unless a recent one exists. Never throws: a failed backup must not stop the app.</summary>
        public static BackupInfo TryAutoBackup(string dbPath, string backupDir, TimeSpan minAge, int keep = 20, DateTime? now = null)
        {
            try
            {
                if (!File.Exists(dbPath))
                    return null;
                var current = now ?? DateTime.Now;
                var latest = List(backupDir).FirstOrDefault(x => !x.IsSafetyCopy);
                if (latest != null && current - latest.Time < minAge)
                    return null;
                return CreateBackup(dbPath, backupDir, keep, current);
            }
            catch
            {
                return null;
            }
        }

        public static BackupInfo CreateBackup(string dbPath, string backupDir, int keep = 20, DateTime? now = null)
        {
            return Copy(dbPath, backupDir, Prefix, keep, now ?? DateTime.Now);
        }

        public static IReadOnlyList<BackupInfo> List(string backupDir)
        {
            if (!Directory.Exists(backupDir))
                return new List<BackupInfo>();

            var result = new List<BackupInfo>();
            foreach (var file in Directory.GetFiles(backupDir, Prefix + "*.db"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var safety = name.StartsWith(SafetyPrefix, StringComparison.Ordinal);
                var stamp = name.Substring(safety ? SafetyPrefix.Length : Prefix.Length);
                if (!DateTime.TryParseExact(stamp, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    continue;
                result.Add(new BackupInfo(file, time, new FileInfo(file).Length, safety));
            }
            return result.OrderByDescending(x => x.Time).ToList();
        }

        /// <summary>True when the file opens as a SQLite database that has the clients table (so it is a SaleManager file).</summary>
        public static bool IsValidBackup(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Clients';";
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Replaces the live database with a backup. The current database is copied first, so a restore can be undone.
        /// All pooled connections are released before the file is overwritten; the caller has to open a new context afterwards.
        /// </summary>
        public static void Restore(string backupPath, string dbPath, string backupDir, int keep = 20, DateTime? now = null)
        {
            if (!IsValidBackup(backupPath))
                throw new InvalidDataException("To nie jest poprawna kopia bazy SaleManager.");

            if (File.Exists(dbPath))
                Copy(dbPath, backupDir, SafetyPrefix, keep, now ?? DateTime.Now);

            SqliteConnection.ClearAllPools();
            var temp = dbPath + ".restore";
            File.Copy(backupPath, temp, true);
            File.Move(temp, dbPath, true);
            foreach (var leftover in new[] { dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
            {
                if (File.Exists(leftover))
                    File.Delete(leftover);
            }
        }

        private static BackupInfo Copy(string dbPath, string backupDir, string prefix, int keep, DateTime now)
        {
            Directory.CreateDirectory(backupDir);

            // Two copies in the same second get the next free second, the name is the only place the time is kept.
            string NameFor(DateTime time) => Path.Combine(backupDir, prefix + time.ToString(TimeFormat, CultureInfo.InvariantCulture) + ".db");
            while (File.Exists(NameFor(now)))
                now = now.AddSeconds(1);
            var target = NameFor(now);

            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }

            Prune(backupDir, prefix, keep);
            return List(backupDir).First(x => x.Path == target);
        }

        // Normal copies and safety copies are counted separately, so frequent copies never push out the safety ones.
        private static void Prune(string backupDir, string prefix, int keep)
        {
            var isSafety = prefix == SafetyPrefix;
            foreach (var old in List(backupDir).Where(x => x.IsSafetyCopy == isSafety).Skip(keep))
            {
                try { File.Delete(old.Path); } catch { /* best effort */ }
            }
        }
    }
}
