using System.Security.Claims;
using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Tests;

public sealed class OwnershipTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public OwnershipTests()
    {
        _connection.Open();
        using var context = As("alice");
        context.Database.EnsureCreated();
        context.Users.AddRange(new IdentityUser("alice") { Id = "alice" }, new IdentityUser("bob") { Id = "bob" });
        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private ApplicationDbContext As(string userId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        return new ApplicationDbContext(options, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });
    }

    private async Task<(GameSystem System, Oracle Oracle, Flow Flow)> EverythingOf(string userId)
    {
        await using var context = As(userId);
        var system = new GameSystem { Name = "Ironsworn", Genre = "Fantasy" };
        var oracle = new Oracle { Name = "Weather", GameSystem = system, DiceCount = 1, DiceFaces = 6 };
        oracle.OracleEntries.Add(new OracleEntry { SortOrder = 1, Result = "Rain", RangeStart = 1, RangeEnd = 6 });
        var flow = new Flow { Name = "Travel", GameSystem = system };
        flow.Steps.Add(new FlowStep { SortOrder = 1, Kind = FlowStepKind.Roll, Oracle = oracle });
        context.AddRange(oracle, flow, new Sheet { FileName = "hero.pdf", GameSystem = system });
        await context.SaveChangesAsync();
        return (system, oracle, flow);
    }

    [Fact]
    public async Task Nobody_sees_another_accounts_items()
    {
        await EverythingOf("alice");

        await using var bob = As("bob");
        Assert.False(await bob.GameSystems.AnyAsync());
        Assert.False(await bob.Oracles.AnyAsync());
        Assert.False(await bob.Flows.AnyAsync());
        Assert.False(await bob.Sheets.AnyAsync());

        await using var alice = As("alice");
        Assert.Equal("alice", (await alice.Oracles.SingleAsync()).OwnerId);
    }

    [Fact]
    public async Task Another_accounts_item_cannot_be_changed()
    {
        var (system, _, _) = await EverythingOf("alice");

        await using var bob = As("bob");
        bob.GameSystems.Update(new GameSystem { ID = system.ID, Name = "Taken", Genre = "Fantasy", OwnerId = "alice" });
        await Assert.ThrowsAsync<OwnershipException>(() => bob.SaveChangesAsync());
    }

    [Fact]
    public async Task Another_accounts_game_system_or_oracle_cannot_be_used()
    {
        var (system, oracle, _) = await EverythingOf("alice");

        await using (var bob = As("bob"))
        {
            bob.Oracles.Add(new Oracle { Name = "Stolen", GameSystemID = system.ID, DiceCount = 1, DiceFaces = 6 });
            var error = await Assert.ThrowsAsync<OwnershipException>(() => bob.SaveChangesAsync());
            Assert.Contains("your own", error.Message);
        }

        await using (var bob = As("bob"))
        {
            var mine = new GameSystem { Name = "Mine", Genre = "Horror" };
            var flow = new Flow { Name = "Borrowed", GameSystem = mine };
            flow.Steps.Add(new FlowStep { SortOrder = 1, Kind = FlowStepKind.Roll, OracleID = oracle.ID });
            bob.Flows.Add(flow);
            await Assert.ThrowsAsync<OwnershipException>(() => bob.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Nobody_sees_another_accounts_entries_steps_or_versions()
    {
        await EverythingOf("alice");

        await using var bob = As("bob");
        Assert.False(await bob.OracleEntries.AnyAsync());
        Assert.False(await bob.FlowSteps.AnyAsync());
        Assert.False(await bob.SheetVersions.AnyAsync());
    }

    [Fact]
    public async Task A_branch_cannot_use_another_accounts_outcome()
    {
        var (_, aliceOracle, _) = await EverythingOf("alice");
        var aliceEntry = aliceOracle.OracleEntries.Single();
        var (_, _, bobFlow) = await EverythingOf("bob");

        await using var bob = As("bob");
        var step = await bob.FlowSteps.SingleAsync(s => s.FlowID == bobFlow.ID);
        bob.FlowBranches.Add(new FlowBranch { FlowStepID = step.ID, OracleEntryID = aliceEntry.ID });
        await Assert.ThrowsAsync<OwnershipException>(() => bob.SaveChangesAsync());
    }

    [Fact]
    public async Task A_step_cannot_be_moved_out_of_another_accounts_flow()
    {
        var (_, _, aliceFlow) = await EverythingOf("alice");
        var (_, _, bobFlow) = await EverythingOf("bob");

        await using var bob = As("bob");
        var aliceStep = await bob.FlowSteps.IgnoreQueryFilters().SingleAsync(s => s.FlowID == aliceFlow.ID);
        aliceStep.FlowID = bobFlow.ID;
        aliceStep.OracleID = null;
        await Assert.ThrowsAsync<OwnershipException>(() => bob.SaveChangesAsync());
    }

    [Fact]
    public async Task The_plain_save_checks_too()
    {
        var (system, _, _) = await EverythingOf("alice");

        await using var bob = As("bob");
        bob.Oracles.Add(new Oracle { Name = "Stolen", GameSystemID = system.ID, DiceCount = 1, DiceFaces = 6 });
        Assert.Throws<OwnershipException>(() => bob.SaveChanges());
    }

    [Fact]
    public async Task Clearing_your_oracles_and_flows_leaves_everyone_elses()
    {
        await EverythingOf("alice");
        await EverythingOf("bob");

        await using (var bob = As("bob"))
        {
            await bob.Flows.ExecuteDeleteAsync();
            await bob.Oracles.ExecuteDeleteAsync();
            Assert.False(await bob.Oracles.AnyAsync());
        }

        await using var alice = As("alice");
        Assert.True(await alice.Oracles.AnyAsync());
        Assert.Single(await alice.FlowSteps.ToListAsync());
    }

    [Fact]
    public async Task Deleting_an_account_deletes_everything_it_owns()
    {
        await EverythingOf("alice");
        await EverythingOf("bob");

        await using (var alice = As("alice"))
        {
            alice.Users.Remove(await alice.Users.SingleAsync(u => u.Id == "alice"));
            await alice.SaveChangesAsync();
        }

        await using var bob = As("bob");
        Assert.Equal(1, await bob.GameSystems.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await bob.Oracles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await bob.OracleEntries.CountAsync());
        Assert.Equal(1, await bob.Flows.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await bob.FlowSteps.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await bob.Sheets.IgnoreQueryFilters().CountAsync());
        Assert.True(await bob.Oracles.AnyAsync());
    }
}
