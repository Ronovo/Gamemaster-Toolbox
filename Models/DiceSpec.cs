using System.Text.RegularExpressions;
using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// TA derived Dice Spec class from the Oracle DiceFaces and NumberOfDice. 
    /// Provides functions like Min And Max, and allowing 
    /// for range spans (i.e. 2d6 = 2-12, 11 Outcomes)
    /// </summary>
    public class DiceSpec
    {
        public static readonly int[] StandardFaces = { 4, 6, 8, 10, 12, 20, 100 };

        private static readonly Regex LabelPattern = new(
            @"^(?:(?<count>\d+)[dD]|[dD])(?<faces>\d+)$",
            RegexOptions.Compiled);

        public int Count { get; }
        public int Faces { get; }

        public DiceSpec(int count, int faces)
        {
            Count = count;
            Faces = faces;
        }

        public int Min => Count;

        public int Max => Count * Faces;

        public int Span => Max - Min + 1;

        public string Label => Count == 1 ? $"d{Faces}" : $"{Count}d{Faces}";

        public static DiceSpec? For(OracleSubtype subtype, int? diceCount, int? diceFaces)
        {
            if (subtype.Category() != OracleCategory.Dice)
            {
                return null;
            }

            if (diceCount == null || diceFaces == null)
            {
                return null;
            }

            return new DiceSpec(diceCount.Value, diceFaces.Value);
        }

        public static DiceSpec? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = LabelPattern.Match(text.Trim());
            if (!match.Success)
            {
                return null;
            }

            var count = match.Groups["count"].Success
                ? int.Parse(match.Groups["count"].Value)
                : 1;
            var faces = int.Parse(match.Groups["faces"].Value);
            if (count < 1 || faces < 2)
            {
                return null;
            }

            return new DiceSpec(count, faces);
        }
    }
}
