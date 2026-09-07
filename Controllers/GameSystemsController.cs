using GM_Toolbox.Data;
using GM_Toolbox.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class GameSystemsController : Controller
{
    private readonly ApplicationDbContext _context;

    public GameSystemsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var systems = await _context.GameSystems
            .OrderBy(g => g.Name)
            .ToListAsync();
        return View(systems);
    }

    public IActionResult Create()
    {
        return View();
    }

    // The duplicate-name check here is half of a pair: the other half is the unique index
    // on GameSystem.Name in ApplicationDbContext. Neither is redundant. This one turns a
    // collision into a sentence the user can act on; the index is what is actually true
    // under a race. Deleting either is a bug.
    //
    // The comparison is case-insensitive because the column is declared COLLATE NOCASE.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Genre,PrimaryOracleType")] GameSystem gameSystem)
    {
        if (await _context.GameSystems.AnyAsync(g => g.Name == gameSystem.Name))
        {
            ModelState.AddModelError(nameof(GameSystem.Name), "A game system with that name already exists.");
        }

        if (ModelState.IsValid)
        {
            _context.Add(gameSystem);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(gameSystem);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var gameSystem = await _context.GameSystems.FindAsync(id);
        if (gameSystem == null) return NotFound();

        return View(gameSystem);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("ID,Name,Genre,PrimaryOracleType")] GameSystem gameSystem)
    {
        if (id != gameSystem.ID) return NotFound();

        if (await _context.GameSystems.AnyAsync(g => g.Name == gameSystem.Name && g.ID != gameSystem.ID))
        {
            ModelState.AddModelError(nameof(GameSystem.Name), "A game system with that name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(gameSystem);
        }

        // Load-then-assign rather than _context.Update() on the bound object, for the same
        // reason as OraclesController.Edit: Update() would write defaults over any property
        // the [Bind] list above does not name.
        var existing = await _context.GameSystems.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = gameSystem.Name;
        existing.Genre = gameSystem.Genre;
        existing.PrimaryOracleType = gameSystem.PrimaryOracleType;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var gameSystem = await _context.GameSystems.FirstOrDefaultAsync(g => g.ID == id);
        if (gameSystem == null) return NotFound();

        ViewData["OracleCount"] = await _context.Oracles.CountAsync(o => o.GameSystemID == id);
        return View(gameSystem);
    }

    // Renamed because the POST would otherwise share a signature with the GET;
    // [ActionName] keeps the route as Delete.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var gameSystem = await _context.GameSystems.FindAsync(id);
        if (gameSystem == null) return RedirectToAction(nameof(Index));

        if (await _context.Oracles.AnyAsync(o => o.GameSystemID == id))
        {
            ViewData["OracleCount"] = await _context.Oracles.CountAsync(o => o.GameSystemID == id);
            ModelState.AddModelError("", "That game system is still used by one or more oracles and cannot be deleted.");
            return View(gameSystem);
        }

        _context.GameSystems.Remove(gameSystem);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
