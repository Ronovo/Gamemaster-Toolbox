using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;
using GM_Toolbox.Models.ViewModels;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;


public class OraclesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly OracleConfigurator _configurator;
    private readonly IWebHostEnvironment _env;
    public OraclesController(ApplicationDbContext context, OracleConfigurator configurator, IWebHostEnvironment env)
    {
        _context = context;
        _configurator = configurator;
        _env = env;
    }

    public async Task<IActionResult> Index()
    {
        return View(await OracleListItems());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var existing = await LoadOracleWithEntries(id);
        if (existing == null) return NotFound();

        return View(existing);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["GameSystemID"] = await GameSystemSelectList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,OracleSubtype,GameSystemID")] Oracle oracle)
    {
        if (ModelState.IsValid)
        {
            _context.Add(oracle);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Configure), new { id = oracle.ID });
        }
        ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
        return View(oracle);
    }

    // Serves the form for editing an oracle's Name, GameSystem and OracleSubtype.
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var oracle = await _context.Oracles
        .FirstOrDefaultAsync(m => m.ID == id);
        if (oracle == null)
        {
            return NotFound();
        }

        ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
        return View(oracle);
    }

    // Changing the subtype deletes every existing entry and sends the user to Configure:
    // a d6's three bands mean nothing on a tarot deck, so there is nothing to carry over.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,OracleSubtype,GameSystemID")] Oracle oracle)
    {
        if (id != oracle.ID)
        {
            return NotFound();
        }

		if (!ModelState.IsValid)
		{
            ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
            return View(oracle);
		}
		
		// Load-then-assign, never _context.Update() on the bound object. Update() marks
		// every scalar modified, so any property missing from the [Bind] list above is
		// written back as its default. That list happens to be complete today; it silently
		// stops being complete the first time someone adds a property and forgets to add
		// it there. That is how DiceSize used to be nulled on every save.
		var existing = await LoadOracleWithEntries(id);
		if (existing == null)
		{
			return NotFound();
		}

		bool subtypeChanged = existing.OracleSubtype != oracle.OracleSubtype;
		
		existing.Name = oracle.Name;
		existing.GameSystemID = oracle.GameSystemID;
		existing.OracleSubtype = oracle.OracleSubtype;
		
		if (subtypeChanged)
		{
			_context.OracleEntries.RemoveRange(existing.OracleEntries.ToList());
		}
		
		await _context.SaveChangesAsync();
		
		if (subtypeChanged)
		{ 
			return RedirectToAction(nameof(Configure), new { id = existing.ID });
		}
		
		return RedirectToAction(nameof(Index));
    }

    // Deleting an oracle takes its entries with it (cascade on the FK).
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var oracle = await _context.Oracles
            .Include(o => o.GameSystem)
            .FirstOrDefaultAsync(m => m.ID == id);
        if (oracle == null)
        {
            return NotFound();
        }

        return View(oracle);
    }

    // Renamed because the POST would otherwise share a signature with the GET;
    // [ActionName] keeps the route as Delete.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var oracle = await _context.Oracles.FindAsync(id);
        if (oracle != null)
        {
            _context.Oracles.Remove(oracle);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // Step 1 of the two-step builder: how many outcomes (dice), or just build the deck (cards).
    public async Task<IActionResult> Configure(int? id, string? returnTo = null)
    {
        if (id == null) return NotFound();

        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        ViewData["ReturnTo"] = returnTo;
        return View(oracle);
    }

    // Reconcile adds, trims or rebuilds entry rows, keeping the result text already typed.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(int id, int? outcomeCount, string? returnTo = null)
    {
        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();
        var error = _configurator.Reconcile(oracle, outcomeCount);
        if (error != null)
        {
            ModelState.AddModelError(nameof(outcomeCount), error);
            ViewData["ReturnTo"] = returnTo;
            return View(oracle);
        }
        
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(EditEntries), new { id });
    }

    // Step 2 of the two-step builder: type the result text, and for dice the band starts.
    public async Task<IActionResult> EditEntries(int? id)
    {
        if (id == null) return NotFound();
        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        var isDice = oracle.OracleSubtype.Category() == OracleCategory.Dice;
        var vm = new EditEntriesViewModel
        {
            OracleID = oracle.ID,
            Name = oracle.Name,
            IsDice = isDice,
            FaceCount = isDice ? oracle.OracleSubtype.FaceCount() : 0,
            Rows = oracle.OracleEntries
           .OrderBy(e => e.SortOrder)
           .Select(e => new EntryRow
           {
               EntryId = e.ID,
               Label = e.CardLabel ?? $"Outcome {e.SortOrder}",
               Result = e.Result,
               StartsAt = e.RangeStart ?? e.SortOrder
           })
           .ToList()
        };

        return View(vm);
    }

    // Validates the band starts, then derives each RangeEnd from them.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEntries(EditEntriesViewModel vm)
    {
        var oracle = await LoadOracleWithEntries(vm.OracleID);
        if (oracle == null) return NotFound();

        var entries = oracle.OracleEntries.OrderBy(e => e.SortOrder).ToList();
        var isDice = oracle.OracleSubtype.Category() == OracleCategory.Dice;

        int faceCount = isDice ? oracle.OracleSubtype.FaceCount() : 0;

        if (isDice)
        {
            for (int i = 0; i < vm.Rows.Count; i++)
            {
                int startsAt = vm.Rows[i].StartsAt;

                if (i == 0 && startsAt != 1)
                    ModelState.AddModelError("", "The first outcome must start at 1.");

                if (startsAt < 1 || startsAt > faceCount)
                    ModelState.AddModelError("", $"Row {i + 1}: start must be between 1 and {faceCount}.");

                if (i > 0 && startsAt <= vm.Rows[i - 1].StartsAt)
                    ModelState.AddModelError("", $"Row {i + 1}: start must be greater than the row above.");
            }
        }

        if (!ModelState.IsValid) 
        {
            // Rebuild the fields the form didn't post back, then re-show
            vm.Name = oracle.Name;
            vm.IsDice = isDice;
            vm.FaceCount = faceCount;
            for (int i = 0; i < vm.Rows.Count && i < entries.Count; i++)
                vm.Rows[i].Label = entries[i].CardLabel ?? $"Outcome {entries[i].SortOrder}";
            return View(vm);
        } 

        for (int i = 0; i < vm.Rows.Count; i++)
        {
            var row = vm.Rows[i];
            var entry = entries.FirstOrDefault(e => e.ID == row.EntryId);
            if (entry == null) continue;

            entry.Result = row.Result ?? string.Empty;

            if (isDice)
            {
                // Bands are derived, not entered. The user gives only each band's start;
                // a band ends one before the next one begins, and the last runs to the
                // die's face count. Starts 1/3/5 on a d6 give 1-2, 3-4, 5-6.
                entry.RangeStart = row.StartsAt;
                entry.RangeEnd = (i < vm.Rows.Count - 1)
                    ? vm.Rows[i + 1].StartsAt - 1
                    : faceCount;
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = vm.OracleID });
    }

    // Asks what to export before exporting it. The download itself is the POST below.
    public async Task<IActionResult> Export()
    {
        return View(new ExportViewModel { Available = await OracleListItems() });
    }

    // Every chosen oracle and its entries as one portable JSON file.
    //
    // A partial file is not a different format — formatVersion stays 1 and it imports into
    // another instance exactly like a full export does. Only the set of oracles differs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(ExportViewModel vm)
    {
        var query = _context.Oracles
            .Include(o => o.OracleEntries)
            .Include(o => o.GameSystem)
            .AsQueryable();

        // SelectedIds is ignored under All, so a tick left behind by the toggle script — or
        // by a browser with no JavaScript, where the list stays enabled — cannot quietly
        // narrow a full export.
        if (vm.Scope == ExportScope.Selected)
        {
            query = query.Where(o => vm.SelectedIds.Contains(o.ID));
        }

        var oracles = await query.OrderBy(o => o.Name).ToListAsync();

        // Nothing ticked, or everything ticked was deleted in another tab: either way there
        // is no file worth producing, and an empty download would look like a successful one.
        if (vm.Scope == ExportScope.Selected && oracles.Count == 0)
        {
            ModelState.AddModelError("", "Pick at least one oracle to export.");
            vm.Available = await OracleListItems();
            return View(vm);
        }

        var file = OracleExporter.Build(oracles);
        var json = JsonSerializer.Serialize(file, OracleJson.Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        // fff = milliseconds, so two exports in the same second do not collide
        var filename = $"gm-toolbox-oracles-{DateTime.Now:yyyy-MM-dd-HHmmssfff}.json";

        await WriteExportArchive(bytes, filename, vm.Scope == ExportScope.Selected);

        return File(bytes, "application/json", filename);
    }

    // Shows what Clear All is about to destroy.
    public async Task<IActionResult> ClearAll()
    {
        var vm = new ClearAllViewModel
        {
            OracleCount = await _context.Oracles.CountAsync(),
            EntryCount = await _context.OracleEntries.CountAsync()
        };

        return View(vm);
    }

    // Destroys everything confirmed in Oracles/ClearAll
    [HttpPost, ActionName("ClearAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllConfirmed()
    {
        // TODO FOR MULTI-USER: filter both deletes to the current user, and drop the reseed
        // entirely — you cannot reseed a table that still holds other people's rows.
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            // Children first: OracleEntries has a required FK to Oracles.
            await _context.OracleEntries.ExecuteDeleteAsync();
            await _context.Oracles.ExecuteDeleteAsync();

            await transaction.CommitAsync();
        }

        // Ensure the database is reseeded, after everything is destroyed.
        //
        // EF's SQLite provider declares int keys as INTEGER PRIMARY KEY AUTOINCREMENT,
        // which parks a high-water mark in the sqlite_sequence table. That mark survives
        // deleting every row, so without this a "cleared" database would hand the next
        // oracle ID 84 instead of 1. Deleting the sqlite_sequence rows resets the counter.
        //
        // Deliberately outside the transaction above: this is not undone by a rollback,
        // so running it inside would let a failure restore the rows and still leave the
        // counter reset — the one combination that breaks the next insert.
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM sqlite_sequence WHERE name IN ('Oracles', 'OracleEntries');");

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("", "Choose a file to import.");
            return View();
        }

        OracleExportFile? parsed;

        try
        {
            using var stream = file.OpenReadStream();
            parsed = await JsonSerializer.DeserializeAsync<OracleExportFile>(stream, OracleJson.Options);
        }
        catch (JsonException ex)
        {
            ModelState.AddModelError("", $"That file is not valid JSON: {ex.Message}");
            return View();
        }

        var existingSystems = await _context.GameSystems.ToListAsync();
        var error = OracleImporter.TryBuild(parsed, existingSystems, out var oracles);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View();
        }

        // MULTI-USER: stamp each oracle with the current user's id before adding.
        //
        // AddRange takes the whole graph — EF inserts each Oracle, reads back its
        // generated ID, and fills in OracleID on its entries automatically. One
        // SaveChanges, so the whole file lands or none of it does.
        _context.Oracles.AddRange(oracles);
        await _context.SaveChangesAsync();

        TempData["ImportMessage"] = $"Imported {oracles.Count} oracle(s).";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------------ helpers

    // Projects rather than .Include()s on purpose. The list needs OracleListItem.NeedsSetup,
    // which calls Category() — a C# switch EF cannot translate to SQL. So the two raw counts
    // it depends on come back from the database and NeedsSetup is worked out in memory.
    // Move NeedsSetup into this query and it fails at runtime, not compile time.
    private Task<List<OracleListItem>> OracleListItems()
    {
        return _context.Oracles
            .OrderBy(o => o.Name)
            .Select(o => new OracleListItem
            {
                ID = o.ID,
                Name = o.Name,
                OracleSubtype = o.OracleSubtype,
                GameSystemName = o.GameSystem!.Name,
                EntryCount = o.OracleEntries.Count,
                HasUnbandedEntries = o.OracleEntries.Any(e => e.RangeStart == null)
            })
            .ToListAsync();
    }

    // Keeps a server-side copy alongside the download. Full and partial exports go to
    // separate folders so a subset is never mistaken for a backup of everything.
    private async Task WriteExportArchive(byte[] bytes, string filename, bool isPartial)
    {
        var folder = Path.Combine(
            _env.ContentRootPath, "App_Data", "Exports", isPartial ? "Partial" : "Full");

        Directory.CreateDirectory(folder);
        // Explicitly reference System.IO.File to avoid ambiguity with the File() method of ControllerBase
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, filename), bytes);
    }

    private Task<Oracle?> LoadOracleWithEntries(int? id)
    {
        return _context.Oracles
            .Include(o => o.OracleEntries)
            .Include(o => o.GameSystem)
            .FirstOrDefaultAsync(o => o.ID == id);
    }

    private async Task<SelectList> GameSystemSelectList(int? selected = null)
    {
        var systems = await _context.GameSystems
            .OrderBy(g => g.Name)
            .ToListAsync();
        return new SelectList(systems, "ID", "Name", selected);
    }
}