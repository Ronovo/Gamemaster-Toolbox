using System.IO.Compression;
using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.ViewModels;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class SheetsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SheetStorageService _storage;

    public SheetsController(ApplicationDbContext context, SheetStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<IActionResult> Index(string? name, int? gameSystemId, int page = 1)
    {
        var vm = await BuildSheetIndex(name, gameSystemId, page);
        if (Request.WantsPartial())
        {
            return PartialView("_SheetListResults", vm);
        }

        return View(vm);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var sheet = await _context.Sheets
            .Include(s => s.Versions)
            .Include(s => s.GameSystem)
            .FirstOrDefaultAsync(s => s.ID == id);
        if (sheet == null) return NotFound();

        return View(sheet);
    }

    public async Task<IActionResult> Upload()
    {
        ViewData["GameSystems"] = FilterSelectLists.GameSystems(await _context.GameSystems.OrderBy(g => g.Name).ToListAsync());
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file, int? gameSystemId)
    {
        var (error, result) = await _storage.SaveUploadAsync(file, gameSystemId, _context.CurrentUserId!);

        if (error != null)
        {
            ModelState.AddModelError("", error);
            ViewData["GameSystems"] = FilterSelectLists.GameSystems(await _context.GameSystems.OrderBy(g => g.Name).ToListAsync());
            return View();
        }

        TempData["UploadMessage"] = result!.Outcome switch
        {
            SheetUploadOutcome.Created => $"Uploaded \"{result.Sheet.FileName}\" as version 1.",
            SheetUploadOutcome.NewVersion => $"\"{result.Sheet.FileName}\" updated to version {result.Version}.",
            SheetUploadOutcome.Unchanged => $"\"{result.Sheet.FileName}\" is unchanged — still at version {result.Version}.",
            _ => null
        };

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Download(int id, int? version)
    {
        var sheet = await _context.Sheets.Include(s => s.Versions).FirstOrDefaultAsync(s => s.ID == id);
        if (sheet == null) return NotFound();

        var targetVersion = version ?? sheet.CurrentVersion;
        var versionRow = sheet.Versions.FirstOrDefault(v => v.Version == targetVersion);
        if (versionRow == null) return NotFound();

        var bytes = await _storage.ReadVersionAsync(sheet.ID, targetVersion);
        var downloadName = targetVersion == sheet.CurrentVersion
            ? sheet.FileName
            : InsertVersionSuffix(sheet.FileName, targetVersion);

        return File(bytes, "application/pdf", downloadName);
    }

    public async Task<IActionResult> ExportAll()
    {
        var sheets = await _context.Sheets.ToListAsync();

        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in sheets)
            {
                var bytes = await _storage.ReadVersionAsync(sheet.ID, sheet.CurrentVersion);
                var entryName = UniqueEntryName(SheetStorageService.SafeFileName(sheet.FileName), usedNames);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(bytes);
            }
        }

        var filename = $"gm-toolbox-sheets-{DateTime.Now:yyyy-MM-dd-HHmmssfff}.zip";
        return File(memory.ToArray(), "application/zip", filename);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var sheet = await _context.Sheets.Include(s => s.Versions).FirstOrDefaultAsync(s => s.ID == id);
        if (sheet == null) return NotFound();

        return View(sheet);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var sheet = await _context.Sheets.FindAsync(id);
        if (sheet == null) return RedirectToAction(nameof(Index));

        _context.Sheets.Remove(sheet);
        await _context.SaveChangesAsync();
        _storage.DeleteSheetFiles(id);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DeleteVersion(int id, int version)
    {
        var sheet = await _context.Sheets.Include(s => s.Versions).FirstOrDefaultAsync(s => s.ID == id);
        if (sheet == null) return NotFound();

        var versionRow = sheet.Versions.FirstOrDefault(v => v.Version == version);
        if (versionRow == null) return NotFound();

        ViewData["Sheet"] = sheet;
        return View(versionRow);
    }

    [HttpPost, ActionName("DeleteVersion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVersionConfirmed(int id, int version)
    {
        var sheet = await _context.Sheets.Include(s => s.Versions).FirstOrDefaultAsync(s => s.ID == id);
        if (sheet == null) return RedirectToAction(nameof(Index));

        // A sheet must always have a current version -- deleting it should go through
        // "Delete" for the whole sheet instead.
        if (version == sheet.CurrentVersion)
        {
            TempData["DetailsError"] = "The current version can't be deleted on its own. Delete the whole sheet instead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var versionRow = sheet.Versions.FirstOrDefault(v => v.Version == version);
        if (versionRow == null) return RedirectToAction(nameof(Details), new { id });

        _context.SheetVersions.Remove(versionRow);
        await _context.SaveChangesAsync();
        _storage.DeleteVersionFile(id, version);

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<SheetIndexViewModel> BuildSheetIndex(string? name, int? gameSystemId, int page)
    {
        var vm = new SheetIndexViewModel();
        await vm.FillAsync(name, gameSystemId, _context.GameSystems, _context.Sheets);
        vm.Results = await SheetListQuery.ToPagedListAsync(SheetListQuery.ApplyFilters(_context.Sheets, vm.Name, vm.GameSystemID), page);
        return vm;
    }

    private static string InsertVersionSuffix(string fileName, int version)
    {
        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{stem} (v{version}){ext}";
    }

    private static string UniqueEntryName(string fileName, HashSet<string> usedNames)
    {
        if (usedNames.Add(fileName))
        {
            return fileName;
        }

        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var counter = 2;
        string candidate;
        do
        {
            candidate = $"{stem} ({counter}){ext}";
            counter++;
        } while (!usedNames.Add(candidate));

        return candidate;
    }
}
