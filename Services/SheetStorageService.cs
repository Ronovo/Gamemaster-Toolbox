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
        private readonly IWebHostEnvironment _env;

        public SheetStorageService(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private string SheetsRoot => Path.Combine(_env.ContentRootPath, "App_Data", "Sheets");

        public async Task<(string? Error, SheetUploadResult? Result)> SaveUploadAsync(IFormFile? file, int? gameSystemId, string ownerId)
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
                .FirstOrDefaultAsync(s => s.OwnerId == ownerId && s.FileName.ToLower() == needle);

            if (existing == null)
            {
                var sheet = new Sheet
                {
                    OwnerId = ownerId,
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
                await _context.SaveChangesAsync();

                await WriteFileAsync(sheet.ID, 1, bytes);

                return (null, new SheetUploadResult { Sheet = sheet, Outcome = SheetUploadOutcome.Created, Version = 1 });
            }

            var currentVersion = existing.Versions.Single(v => v.Version == existing.CurrentVersion);
            if (currentVersion.Checksum == checksum)
            {
                // Same content, same filename: not a real change. DateUpdated should mean
                // "content changed", not "someone re-uploaded the same bytes", so it is
                // deliberately left untouched here.
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

            await _context.SaveChangesAsync();
            await WriteFileAsync(existing.ID, newVersionNumber, bytes);

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
            return Path.Combine(SheetsRoot, sheetId.ToString(), $"{version}.pdf");
        }

        public Task<byte[]> ReadVersionAsync(int sheetId, int version)
        {
            return System.IO.File.ReadAllBytesAsync(GetFilePath(sheetId, version));
        }

        public void DeleteSheetFiles(int sheetId)
        {
            var folder = Path.Combine(SheetsRoot, sheetId.ToString());
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

        private async Task WriteFileAsync(int sheetId, int version, byte[] bytes)
        {
            var folder = Path.Combine(SheetsRoot, sheetId.ToString());
            Directory.CreateDirectory(folder);
            await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, $"{version}.pdf"), bytes);
        }
    }
}
