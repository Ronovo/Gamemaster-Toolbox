using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public static class FilterSelectLists
    {
        /// <summary>The game system picker on the oracle and flow forms.</summary>
        public static async Task<SelectList> GameSystemPickerAsync(IQueryable<GameSystem> systems, int? selected = null)
        {
            return new SelectList(await systems.OrderBy(g => g.Name).ToListAsync(), "ID", "Name", selected);
        }

        public static List<SelectListItem> GameSystems(IEnumerable<GameSystem> systems)
        {
            var items = new List<SelectListItem>
            {
                new() { Value = "", Text = "All game systems" }
            };

            items.AddRange(systems.Select(s => new SelectListItem
            {
                Value = s.ID.ToString(),
                Text = s.Name
            }));

            return items;
        }

        public static List<SelectListItem> OracleTypes(IEnumerable<(int Count, int Faces)> extraDice)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<SelectListItem>
            {
                new() { Value = "", Text = "All types" },
                new() { Value = nameof(OracleSubtype.Dice), Text = OracleSubtype.Dice.DisplayName() }
            };

            void AddDice(int count, int faces)
            {
                var label = new DiceSpec(count, faces).Label;
                if (!seen.Add(label))
                {
                    return;
                }

                items.Add(new SelectListItem { Value = label, Text = label });
            }

            foreach (var faces in DiceSpec.StandardFaces)
            {
                AddDice(1, faces);
            }

            foreach (var dice in extraDice.OrderBy(d => d.Count).ThenBy(d => d.Faces))
            {
                AddDice(dice.Count, dice.Faces);
            }

            items.Add(new SelectListItem
            {
                Value = nameof(OracleSubtype.PlayingCards),
                Text = OracleSubtype.PlayingCards.DisplayName()
            });
            items.Add(new SelectListItem
            {
                Value = nameof(OracleSubtype.TarotCards),
                Text = OracleSubtype.TarotCards.DisplayName()
            });

            return items;
        }
    }
}
