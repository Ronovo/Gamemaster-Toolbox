using System.Security.Cryptography;
using GM_Toolbox.Data;
using GM_Toolbox.Models;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public enum SheetUploadOutcome
    {
        Created,
        Unchanged,
        NewVersion
    }

    public class SheetUploadResult
    {
        public Sheet Sheet { get; set; } = null!;
        public SheetUploadOutcome Outcome { get; set; }
        public int Version { get; set; }
    }

    public class SheetStorageService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _sheetsRoot;

        public SheetStorageService(ApplicationDbContext context, IWebHostEnvironment env, IConfiguration config)
        {
            _context = context;
            _sheetsRoot = config["SheetsRoot"] ?? Path.Combine(env.ContentRootPath, "App_Data", "Sheets");
        }

        public async Task<(string? Error, SheetUploadResult? Result)> SaveUploadAsync(IFormFile? file, int? gameSystemId)
        {
            if (file == null || file.Length == 0)
            {
                return ("Choose a file to upload.", null);
            }

            byte[] bytes;
            using (var stream = file.OpenReadStream())
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            var validationError = SheetFileValidator.Validate(bytes.LongLength, bytes);
            if (validationError != null)
            {
                return (validationError, null);
            }

            if (gameSystemId is <= 0)
            {
                gameSystemId = null;
            }

            var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var fileName = SafeFileName(file.FileName);
            var now = DateTime.UtcNow;

            // Case-insensitive exact filename match among the uploader's own sheets, no other
            // normalization -- keeps the versioning rule simple and predictable.
            var needle = fileName.ToLower();
            var existing = await _context.Sheets
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.FileName.ToLower() == needle);

            if (existing == null)
            {
                var sheet = new Sheet
                {
                    FileName = fileName,
                    GameSystemID = gameSystemId,
                    DateUploaded = now,
                    DateUpdated = now,
                    CurrentVersion = 1
                };
                sheet.Versions.Add(new SheetVersion
                {
                    Version = 1,
                    Checksum = checksum,
                    FileSizeBytes = bytes.LongLength,
                    UploadedAt = now,
                    OriginalFileName = file.FileName
                });

                _context.Sheets.Add(sheet);
                await SaveWithFileAsync(sheet, 1, bytes);

                return (null, new SheetUploadResult { Sheet = sheet, Outcome = SheetUploadOutcome.Created, Version = 1 });
            }

            existing.GameSystemID = gameSystemId;
            var currentVersion = existing.Versions.Single(v => v.Version == existing.CurrentVersion);
            if (currentVersion.Checksum == checksum)
            {
                // Same content, same filename: not a new version. DateUpdated means "content changed",
                // so only the game system is saved.
                await _context.SaveChangesAsync();
                return (null, new SheetUploadResult { Sheet = existing, Outcome = SheetUploadOutcome.Unchanged, Version = existing.CurrentVersion });
            }

            var newVersionNumber = existing.CurrentVersion + 1;
            existing.Versions.Add(new SheetVersion
            {
                SheetID = existing.ID,
                Version = newVersionNumber,
                Checksum = checksum,
                FileSizeBytes = bytes.LongLength,
                UploadedAt = now,
                OriginalFileName = file.FileName
            });
            existing.CurrentVersion = newVersionNumber;
            existing.DateUpdated = now;

            await SaveWithFileAsync(existing, newVersionNumber, bytes);

            return (null, new SheetUploadResult { Sheet = existing, Outcome = SheetUploadOutcome.NewVersion, Version = newVersionNumber });
        }

        /// <summary>The name without any folder part, so it is safe to use as a zip entry or download name.</summary>
        public static string SafeFileName(string name)
        {
            var bare = Path.GetFileName(name.Replace('\\', '/')).Trim();
            return bare is "" or "." or ".." ? "sheet.pdf" : bare;
        }

        public string GetFilePath(int sheetId, int version)
        {
            return Path.Combine(_sheetsRoot, sheetId.ToString(), $"{version}.pdf");
        }

        public Task<byte[]> ReadVersionAsync(int sheetId, int version)
        {
            return System.IO.File.ReadAllBytesAsync(GetFilePath(sheetId, version));
        }

        public void DeleteSheetFiles(int sheetId)
        {
            var folder = Path.Combine(_sheetsRoot, sheetId.ToString());
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        public void DeleteVersionFile(int sheetId, int version)
        {
            var path = GetFilePath(sheetId, version);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        // The row and its file are saved together: if the file can't be written, the row is rolled back.
        private async Task SaveWithFileAsync(Sheet sheet, int version, byte[] bytes)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            await _context.SaveChangesAsync();
            Directory.CreateDirectory(Path.Combine(_sheetsRoot, sheet.ID.ToString()));
            await System.IO.File.WriteAllBytesAsync(GetFilePath(sheet.ID, version), bytes);
            await transaction.CommitAsync();
        }
    }
}
