// ============================================================================
// REFERENCE FILE: File Hash Utility for Incremental Indexing
// Complete implementation for detecting changed files.
// ============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyApp.CodeAnalysis.Reference
{
    /// <summary>
    /// Utility class for computing file hashes for incremental indexing.
    ///
    /// WHY FILE HASHING?
    /// 1. Skip unchanged files during re-indexing
    /// 2. Detect modifications even if timestamp is unreliable
    /// 3. Enable efficient incremental updates
    ///
    /// USAGE IN INDEXER:
    /// 1. Before indexing a file, compute its hash
    /// 2. Compare with stored hash from previous indexing
    /// 3. If hashes match, skip the file
    /// 4. If different, re-index and update stored hash
    /// </summary>
    public static class FileHashUtility
    {
        /// <summary>
        /// Computes SHA256 hash of a file's contents.
        /// Returns a 64-character hex string.
        /// </summary>
        /// <param name="filePath">Full path to the file</param>
        /// <returns>Lowercase hex string of SHA256 hash</returns>
        public static string ComputeFileHash(string filePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filePath);

            byte[] hashBytes = sha256.ComputeHash(stream);
            return BytesToHexString(hashBytes);
        }

        /// <summary>
        /// Computes SHA256 hash of a file asynchronously.
        /// </summary>
        public static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct = default)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,  // 80KB buffer for performance
                useAsync: true);

            byte[] hashBytes = await sha256.ComputeHashAsync(stream, ct);
            return BytesToHexString(hashBytes);
        }

        /// <summary>
        /// Computes hash of a string (for testing or in-memory content).
        /// </summary>
        public static string ComputeStringHash(string content)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            byte[] hashBytes = sha256.ComputeHash(contentBytes);
            return BytesToHexString(hashBytes);
        }

        /// <summary>
        /// Computes a combined hash for a directory (all .cs files).
        /// Useful for project-level change detection.
        /// </summary>
        public static string ComputeDirectoryHash(string directoryPath, string pattern = "*.cs")
        {
            using SHA256 sha256 = SHA256.Create();
            using MemoryStream combinedStream = new MemoryStream();

            string[] files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);  // Consistent ordering

            foreach (string file in files)
            {
                // Include relative path in hash (catches renames/moves)
                string relativePath = Path.GetRelativePath(directoryPath, file);
                byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
                combinedStream.Write(pathBytes, 0, pathBytes.Length);

                // Include file hash
                string fileHash = ComputeFileHash(file);
                byte[] hashBytes = Encoding.UTF8.GetBytes(fileHash);
                combinedStream.Write(hashBytes, 0, hashBytes.Length);
            }

            combinedStream.Position = 0;
            byte[] finalHash = sha256.ComputeHash(combinedStream);
            return BytesToHexString(finalHash);
        }

        /// <summary>
        /// Checks if a file has changed since last indexing.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="storedHash">Hash from previous indexing (can be null)</param>
        /// <returns>True if file has changed or was never indexed</returns>
        public static bool HasFileChanged(string filePath, string? storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return true;  // Never indexed
            }

            if (!File.Exists(filePath))
            {
                return true;  // File was deleted
            }

            string currentHash = ComputeFileHash(filePath);
            return !string.Equals(currentHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Async version of HasFileChanged.
        /// </summary>
        public static async Task<bool> HasFileChangedAsync(
            string filePath,
            string? storedHash,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return true;
            }

            if (!File.Exists(filePath))
            {
                return true;
            }

            string currentHash = await ComputeFileHashAsync(filePath, ct);
            return !string.Equals(currentHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Converts byte array to lowercase hex string.
        /// </summary>
        private static string BytesToHexString(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));  // Lowercase hex
            }
            return builder.ToString();
        }
    }

    /// <summary>
    /// Extension methods for using file hashing in the indexer.
    /// </summary>
    public static class FileHashExtensions
    {
        /// <summary>
        /// Gets file info including hash for incremental indexing.
        /// </summary>
        public static FileIndexInfo GetIndexInfo(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);

            return new FileIndexInfo
            {
                Path = filePath,
                Hash = FileHashUtility.ComputeFileHash(filePath),
                ModificationTime = fileInfo.LastWriteTimeUtc,
                Size = fileInfo.Length
            };
        }

        /// <summary>
        /// Async version of GetIndexInfo.
        /// </summary>
        public static async Task<FileIndexInfo> GetIndexInfoAsync(
            string filePath,
            CancellationToken ct = default)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            string hash = await FileHashUtility.ComputeFileHashAsync(filePath, ct);

            return new FileIndexInfo
            {
                Path = filePath,
                Hash = hash,
                ModificationTime = fileInfo.LastWriteTimeUtc,
                Size = fileInfo.Length
            };
        }
    }

    /// <summary>
    /// File information for indexing decisions.
    /// </summary>
    public class FileIndexInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTime ModificationTime { get; set; }
        public long Size { get; set; }
    }

    // =========================================================================
    // EXAMPLE USAGE IN INDEXER
    // =========================================================================
    /*
    public async Task<bool> ShouldIndexFileAsync(
        long snapshotId,
        string filePath,
        CancellationToken ct)
    {
        // Get stored file info from previous indexing
        SourceFile? existingFile = await _repository.GetFileAsync(snapshotId, filePath, ct);

        if (existingFile == null)
        {
            // New file, needs indexing
            return true;
        }

        // Check if file has changed
        bool hasChanged = await FileHashUtility.HasFileChangedAsync(
            filePath,
            existingFile.FileHash,
            ct);

        if (hasChanged)
        {
            _logger.LogInformation("File changed, will re-index: {FilePath}", filePath);
        }
        else
        {
            _logger.LogDebug("File unchanged, skipping: {FilePath}", filePath);
        }

        return hasChanged;
    }

    public async Task IndexFileAsync(long snapshotId, string filePath, CancellationToken ct)
    {
        // Get file info including hash
        FileIndexInfo info = await FileHashExtensions.GetIndexInfoAsync(filePath, ct);

        // Record file with hash for future incremental checks
        long fileId = await _repository.RecordFileAsync(
            snapshotId,
            filePath,
            "csharp",
            info.Hash,  // Store hash for incremental indexing
            ct);

        // ... continue with Roslyn analysis
    }
    */
}
