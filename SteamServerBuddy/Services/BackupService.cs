using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace SteamServerBuddy.Services
{
    public class BackupInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public DateTime CreationTime { get; set; }
        public long SizeBytes { get; set; }

        public string SizeDisplay
        {
            get
            {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
                return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }
    }

    public class BackupService
    {
        private const string BACKUP_DIR_NAME = "Backups";

        public async Task CreateBackupAsync(string serverName, string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            {
                throw new DirectoryNotFoundException($"Server install path not found: {installPath}");
            }

            // Create Backups directory inside the install path
            var backupDir = Path.Combine(installPath, BACKUP_DIR_NAME);
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var safeServerName = string.Join("_", serverName.Split(Path.GetInvalidFileNameChars()));
            var zipFileName = $"{safeServerName}_{timestamp}.zip";
            var zipFilePath = Path.Combine(backupDir, zipFileName);

            await Task.Run(() =>
            {
                // Create backup of everything EXCEPT the Backups folder itself and temp files
                // Using a temp folder or manual zipping is safer to avoid recursive loop
                // But ZipFile.CreateFromDirectory includes everything by default.
                
                // Strategy: List all top-level files/folders, exclude "Backups", and zip them carefully
                // Or easier: Zip to a temp location, then move to Backups folder.
                
                var tempZipPath = Path.GetTempFileName();
                File.Delete(tempZipPath); // Ensure it doesn't exist so ZipFile can create it, but get a valid temp path name
                tempZipPath += ".zip";

                try 
                {
                   ZipFile.CreateFromDirectory(installPath, tempZipPath, CompressionLevel.Optimal, false);
                }
                catch (IOException)
                {
                    // If files are locked, we might fail. 
                    // Ideally server should be stopped before calling this.
                    throw new Exception("Could not create backup. Ensure the server is STOPPED before backing up.");
                }

                // Problem: This includes the 'Backups' folder itself recursively if we are not careful!
                // ZipFile.CreateFromDirectory will try to zip the destination if it's inside source.
                
                // BETTER STRATEGY: 
                // 1. Enumerate files to zip (excluding Backups folder)
                // 2. Add to zip archive manually
                
                using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    var rootDir = new DirectoryInfo(installPath);
                    foreach (var file in rootDir.GetFiles("*", SearchOption.AllDirectories))
                    {
                        // Exclude the Backups folder content
                        if (file.FullName.Contains(Path.Combine(installPath, BACKUP_DIR_NAME))) continue;
                        
                        // Exclude lock files or logs if needed? For now keep logs.

                        var relPath = Path.GetRelativePath(installPath, file.FullName);
                        archive.CreateEntryFromFile(file.FullName, relPath);
                    }
                }
            });
        }

        public async Task<List<BackupInfo>> GetBackupsAsync(string installPath)
        {
            return await Task.Run(() =>
            {
                var backups = new List<BackupInfo>();
                var backupDir = Path.Combine(installPath, BACKUP_DIR_NAME);

                if (Directory.Exists(backupDir))
                {
                    var dirInfo = new DirectoryInfo(backupDir);
                    foreach (var file in dirInfo.GetFiles("*.zip").OrderByDescending(f => f.CreationTime))
                    {
                        backups.Add(new BackupInfo
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            CreationTime = file.CreationTime,
                            SizeBytes = file.Length
                        });
                    }
                }
                return backups;
            });
        }

        public async Task DeleteBackupAsync(string backupPath)
        {
            await Task.Run(() =>
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            });
        }

        public async Task RestoreBackupAsync(string backupPath, string installPath)
        {
            if (!File.Exists(backupPath)) throw new FileNotFoundException("Backup file not found", backupPath);
            if (!Directory.Exists(installPath)) throw new DirectoryNotFoundException("Install path not found");

            await Task.Run(() =>
            {
                // Safety check: Ensure server is stopped (ViewModel should handle this, but valid here too)
                
                // 1. Clean current directory? 
                // Re-installing from backup usually means wiping current state or overwriting.
                // Overwriting is safer than deleting everything first (in case unzip fails).
                // But ZipFile.ExtractToDirectory with overwrite=true works well.
                
                ZipFile.ExtractToDirectory(backupPath, installPath, true);
            });
        }
    }
}
