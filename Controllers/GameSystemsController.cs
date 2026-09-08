using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
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

        var query = _context.GameSystems.AsQueryable();
        if (nameTerm != null)
        {
            var needle = nameTerm.ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(needle));
        }

        if (genreTerm != null)
        {
            var needle = genreTerm.ToLower();
            query = query.Where(g => g.Genre.ToLower().Contains(needle));
        }

        var totalCount = await query.CountAsync();
        page = PagedList<GameSystem>.ClampPage(page, totalCount);
        var items = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * PagedList<GameSystem>.DefaultPageSize)
            .Take(PagedList<GameSystem>.DefaultPageSize)
            .ToListAsync();

        var vm = new GameSystemIndexViewModel
        {
            Name = nameTerm,
            Genre = genreTerm,
            CatalogEmpty = !await _context.GameSystems.AnyAsync(),
            Results = new PagedList<GameSystem>
            {
                Items = items,
                Page = page,
                PageSize = PagedList<GameSystem>.DefaultPageSize,
                TotalCount = totalCount
            }
        };

        if (string.Equals(Request.Query["partial"], "1", StringComparison.OrdinalIgnoreCase))
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

        // Load-then-assign rather than _context.Update() on the bound object, for the same
        // reason as OraclesController.Edit: Update() would write defaults over any property
        // the [Bind] list above does not name.
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

    // Renamed because the POST would otherwise share a signature with the GET;
    // [ActionName] keeps the route as Delete.
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
