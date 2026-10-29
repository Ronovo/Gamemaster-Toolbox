using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using GM_Toolbox.Services;
using Microsoft.EntityFrameworkCore;

public class GameSystemsController : Controller
{
    private readonly ApplicationDbContext _context;

    public GameSystemsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? name, string? genre, int page = 1)
    {
        var nameTerm = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var genreTerm = string.IsNullOrWhiteSpace(genre) ? null : genre.Trim();

        var query = _context.GameSystems.Containing(g => g.Name, nameTerm).Containing(g => g.Genre, genreTerm);

        var vm = new GameSystemIndexViewModel
        {
            Name = nameTerm,
            Genre = genreTerm,
            CatalogEmpty = !await _context.GameSystems.AnyAsync(),
            Results = await PagedList<GameSystem>.CreateAsync(query.OrderBy(g => g.Name), systems => systems, page)
        };

        if (Request.WantsPartial())
        {
            return PartialView("_GameSystemListResults", vm);
        }

        return View(vm);
    }

    public IActionResult Create()
    {
        return View();
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Genre")] GameSystem gameSystem)
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
    public async Task<IActionResult> Edit(int id, [Bind("ID,Name,Genre")] GameSystem gameSystem)
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

        // Load-then-assign rather than _context.Update() on the bound object: Update() would write
        // defaults over any property the [Bind] list above does not name.
        var existing = await _context.GameSystems.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = gameSystem.Name;
        existing.Genre = gameSystem.Genre;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var gameSystem = await _context.GameSystems.FirstOrDefaultAsync(g => g.ID == id);
        if (gameSystem == null) return NotFound();

        ViewData["OracleCount"] = await _context.Oracles.CountAsync(o => o.GameSystemID == id);
        ViewData["FlowCount"] = await _context.Flows.CountAsync(f => f.GameSystemID == id);
        return View(gameSystem);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var gameSystem = await _context.GameSystems.FindAsync(id);
        if (gameSystem == null) return RedirectToAction(nameof(Index));

        var oracleCount = await _context.Oracles.CountAsync(o => o.GameSystemID == id);
        var flowCount = await _context.Flows.CountAsync(f => f.GameSystemID == id);

        if (oracleCount > 0 || flowCount > 0)
        {
            ViewData["OracleCount"] = oracleCount;
            ViewData["FlowCount"] = flowCount;
            ModelState.AddModelError("", oracleCount > 0 && flowCount > 0
                ? "That game system is still used by one or more oracles and flows, and cannot be deleted."
                : oracleCount > 0
                    ? "That game system is still used by one or more oracles and cannot be deleted."
                    : "That game system is still used by one or more flows and cannot be deleted.");
            return View(gameSystem);
        }

        _context.GameSystems.Remove(gameSystem);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
