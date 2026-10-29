using GM_Toolbox.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GM_Toolbox.Data
{
    /// <summary>
    /// A save that breaks a rule in <see cref="OwnershipRules"/>. The message is shown to the user.
    /// </summary>
    public class OwnershipException(string message) : Exception(message);

    /// <summary>
    /// Checked on every SaveChanges: anything added, changed or deleted must belong to the signed-in account, and
    /// so must everything it points at, before and after the change. New items belong to the account that made
    /// them. Bulk ExecuteDelete calls skip SaveChanges, so they only run on the account's filtered sets.
    /// </summary>
    internal class OwnershipRules(ApplicationDbContext context)
    {
        private const string NotYours = "Only the account that made this can change it.";
        private const string NotYourReference = "You can only use your own game systems, oracles and steps.";

        private static readonly HashSet<Type> Guarded =
        [
            typeof(GameSystem), typeof(Oracle), typeof(OracleEntry), typeof(Sheet), typeof(SheetVersion),
            typeof(Flow), typeof(FlowStep), typeof(FlowChoiceOption), typeof(FlowBranch)
        ];

        public async Task CheckAsync(CancellationToken ct)
        {
            var changed = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                            && Guarded.Contains(e.Entity.GetType()))
                .ToList();
            if (changed.Count == 0)
            {
                return;
            }

            var userId = context.CurrentUserId ?? throw new OwnershipException("Log in to make changes.");

            foreach (var entry in changed.Where(e => e.Entity is IOwned))
            {
                var owned = (IOwned)entry.Entity;
                if (entry.State == EntityState.Added)
                {
                    owned.OwnerId ??= userId;
                    CheckOwner(owned.OwnerId, userId, NotYours);
                }
                else
                {
                    var original = entry.OriginalValues.GetValue<string?>(nameof(IOwned.OwnerId));
                    CheckOwner(original, userId, NotYours);
                    owned.OwnerId = original;
                }
            }

            foreach (var entry in changed)
            {
                switch (entry.Entity)
                {
                    case OracleEntry:
                        await RequireParentAsync<Oracle>(entry, nameof(OracleEntry.OracleID), userId, ct);
                        break;
                    case SheetVersion:
                        await RequireParentAsync<Sheet>(entry, nameof(SheetVersion.SheetID), userId, ct);
                        break;
                    case FlowStep:
                        await RequireParentAsync<Flow>(entry, nameof(FlowStep.FlowID), userId, ct);
                        break;
                    case FlowChoiceOption:
                        await RequireStepsAsync(Keys(entry, nameof(FlowChoiceOption.FlowStepID)), userId, NotYours, ct);
                        break;
                    case FlowBranch:
                        await RequireStepsAsync(Keys(entry, nameof(FlowBranch.FlowStepID)), userId, NotYours, ct);
                        break;
                }

                if (entry.State == EntityState.Deleted)
                {
                    continue;
                }
                switch (entry.Entity)
                {
                    case Oracle:
                        await RequireMineAsync<GameSystem>(Key(entry, nameof(Oracle.GameSystemID)), userId, NotYourReference, ct);
                        break;
                    case Flow:
                        await RequireMineAsync<GameSystem>(Key(entry, nameof(Flow.GameSystemID)), userId, NotYourReference, ct);
                        break;
                    case Sheet:
                        await RequireMineAsync<GameSystem>(Key(entry, nameof(Sheet.GameSystemID)), userId, NotYourReference, ct);
                        break;
                    case FlowStep:
                        await RequireMineAsync<Oracle>(Key(entry, nameof(FlowStep.OracleID)), userId, NotYourReference, ct);
                        await RequireStepsAsync([Key(entry, nameof(FlowStep.SourceStepID))], userId, NotYourReference, ct);
                        break;
                    case FlowChoiceOption:
                        await RequireMineAsync<Oracle>(Key(entry, nameof(FlowChoiceOption.OracleID)), userId, NotYourReference, ct);
                        await RequireStepsAsync([Key(entry, nameof(FlowChoiceOption.TargetStepID))], userId, NotYourReference, ct);
                        break;
                    case FlowBranch:
                        await RequireMineAsync<Oracle>(Key(entry, nameof(FlowBranch.OracleID)), userId, NotYourReference, ct);
                        await RequireStepsAsync([Key(entry, nameof(FlowBranch.TargetStepID))], userId, NotYourReference, ct);
                        await RequireMineAsync<Oracle>(
                            await ParentKeyAsync<OracleEntry>(Key(entry, nameof(FlowBranch.OracleEntryID)), nameof(OracleEntry.OracleID), ct),
                            userId, NotYourReference, ct);
                        break;
                }
            }
        }

        private static void CheckOwner(string? ownerId, string userId, string message)
        {
            if (ownerId != userId)
            {
                throw new OwnershipException(message);
            }
        }

        // Keys are read through the change tracker: something added in this same save (an import) only has a
        // temporary key, which EF keeps there rather than on the object.
        private static object? Key(EntityEntry entry, string property) => entry.Property(property).CurrentValue;

        // A changed child is checked against the parent it had and the one it moves to.
        private static object?[] Keys(EntityEntry entry, string property) =>
            entry.State == EntityState.Modified
                ? [entry.Property(property).OriginalValue, Key(entry, property)]
                : [Key(entry, property)];

        private async Task RequireParentAsync<T>(EntityEntry entry, string property, string userId, CancellationToken ct)
            where T : class, IOwned
        {
            foreach (var id in Keys(entry, property))
            {
                await RequireMineAsync<T>(id, userId, NotYours, ct);
            }
        }

        private async Task RequireStepsAsync(object?[] stepIds, string userId, string message, CancellationToken ct)
        {
            foreach (var stepId in stepIds)
            {
                await RequireMineAsync<Flow>(await ParentKeyAsync<FlowStep>(stepId, nameof(FlowStep.FlowID), ct), userId, message, ct);
            }
        }

        private EntityEntry<T>? TrackedById<T>(object id) where T : class =>
            context.ChangeTracker.Entries<T>().FirstOrDefault(e => Equals(e.Property("ID").CurrentValue, id));

        private async Task RequireMineAsync<T>(object? id, string userId, string message, CancellationToken ct)
            where T : class, IOwned
        {
            if (id == null)
            {
                return;
            }
            var owner = TrackedById<T>(id)?.Entity.OwnerId
                ?? await context.Set<T>().IgnoreQueryFilters()
                    .Where(e => EF.Property<int>(e, "ID") == (int)id)
                    .Select(e => e.OwnerId)
                    .FirstOrDefaultAsync(ct);
            CheckOwner(owner, userId, message);
        }

        // The key a child row points its parent with. A child that doesn't exist gives 0, which nobody owns.
        private async Task<object?> ParentKeyAsync<T>(object? id, string property, CancellationToken ct) where T : class
        {
            if (id == null)
            {
                return null;
            }
            var tracked = TrackedById<T>(id);
            return tracked != null
                ? Key(tracked, property)
                : await context.Set<T>().IgnoreQueryFilters()
                    .Where(e => EF.Property<int>(e, "ID") == (int)id)
                    .Select(e => EF.Property<int>(e, property))
                    .FirstOrDefaultAsync(ct);
        }
    }
}
