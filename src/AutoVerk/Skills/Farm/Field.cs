using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoVerk.Engine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>
    /// A named planting field: lines of LineLength spots along the facing direction, stacked to the right, starting
    /// a metre ahead of where the player stood. Saved per world; the spots are worked out from it. Plain C#.
    /// </summary>
    public sealed class Field
    {
        public const int MaxCount = 999;
        private const double Start = 1.0;

        public string Name;
        /// <summary>The seed item, as the game names its prefab: CarrotSeeds.</summary>
        public string Seed;
        /// <summary>Where the player stood.</summary>
        public double X, Y, Z;
        /// <summary>The player's facing on the ground, unit length.</summary>
        public double ForwardX, ForwardZ;
        public int Count;
        public int LineLength;
        public double Spacing;
        public bool Staggered;

        /// <summary>Between lines: Spacing, or Spacing·√3/2 staggered (every neighbour Spacing away).</summary>
        public double LineSpacing => Staggered ? Spacing * Math.Sqrt(3) / 2 : Spacing;

        /// <summary>Why it can't be used, or null.</summary>
        public string Validate()
        {
            if (string.IsNullOrEmpty(Name)) return "no name";
            if (string.IsNullOrEmpty(Seed)) return "no seed";
            if (Count < 1 || Count > MaxCount) return $"count must be 1..{MaxCount}";
            if (LineLength < 1) return "line must be 1 or more";
            if (!(Spacing > 0.2 && Spacing < 10)) return "spacing must be 0.2..10 m";
            if (Math.Abs(Math.Sqrt(ForwardX * ForwardX + ForwardZ * ForwardZ) - 1) > 0.01) return "forward must be unit length";
            return null;
        }

        /// <summary>
        /// The spots in order along a serpentine path (line 0 forward, line 1 back, …), so the next spot is always
        /// a neighbour. Staggered odd lines shift half a spot. Each comes with the way the path goes there.
        /// </summary>
        public List<Spot> Spots()
        {
            // Lines stack to the player's right: Unity's Cross(up, forward) = (fz, -fx).
            double rx = ForwardZ, rz = -ForwardX;
            int length = Math.Min(LineLength, Count);
            var spots = new List<Spot>(Count);
            for (int i = 0; i < Count; i++)
            {
                int line = i / length;
                int k = i % length;
                bool back = line % 2 == 1;
                int col = back ? length - 1 - k : k;
                double along = Start + col * Spacing + (Staggered && back ? Spacing / 2 : 0);
                double side = line * LineSpacing;
                double dir = back ? -1 : 1;
                spots.Add(new Spot
                {
                    Index = i,
                    X = X + ForwardX * along + rx * side,
                    Z = Z + ForwardZ * along + rz * side,
                    PathX = ForwardX * dir,
                    PathZ = ForwardZ * dir,
                });
            }
            return spots;
        }

        /// <summary>One line of the fields file: name=carrots-1 seed=CarrotSeeds origin=1,2,3 …</summary>
        public string ToLine() => string.Join(" ",
            "field",
            $"name={Name}",
            $"seed={Seed}",
            $"origin={N(X)},{N(Y)},{N(Z)}",
            $"forward={N(ForwardX)},{N(ForwardZ)}",
            $"count={Count}",
            $"line={LineLength}",
            $"spacing={N(Spacing)}",
            $"layout={(Staggered ? "staggered" : "grid")}");

        /// <summary>A field from its line, or null if the line isn't a valid field.</summary>
        public static Field FromLine(string line)
        {
            if (Args.ParseLine(line, out var v, out _) != "field") return null;
            try
            {
                var origin = Numbers(v["origin"]);
                var forward = Numbers(v["forward"]);
                var f = new Field
                {
                    Name = v["name"],
                    Seed = v["seed"],
                    X = origin[0], Y = origin[1], Z = origin[2],
                    ForwardX = forward[0], ForwardZ = forward[1],
                    Count = int.Parse(v["count"], CultureInfo.InvariantCulture),
                    LineLength = int.Parse(v["line"], CultureInfo.InvariantCulture),
                    Spacing = double.Parse(v["spacing"], CultureInfo.InvariantCulture),
                    Staggered = v["layout"] == "staggered",
                };
                return f.Validate() == null ? f : null;
            }
            catch (Exception e) when (e is KeyNotFoundException || e is FormatException || e is IndexOutOfRangeException)
            {
                return null;
            }
        }

        private static double[] Numbers(string text) =>
            text.Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

        private static string N(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public struct Spot
    {
        public int Index;
        public double X, Z;
        /// <summary>The way the path walks through this spot, unit length.</summary>
        public double PathX, PathZ;
    }

    /// <summary>One world's fields, a line each.</summary>
    public sealed class FieldBook
    {
        public List<Field> Fields = new List<Field>();

        public Field Find(string name) =>
            Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

        public string ToText() => string.Join("\n", Fields.Select(f => f.ToLine())) + "\n";

        /// <summary>The fields in a file's text; lines that aren't valid fields are left out.</summary>
        public static FieldBook FromText(string text) => new FieldBook
        {
            Fields = (text ?? "").Split('\n').Select(Field.FromLine).Where(f => f != null).ToList(),
        };

        /// <summary>"carrots-3": the crop in lower-case plural, then one more than the highest number in use.</summary>
        public string NextName(string crop)
        {
            var stem = (crop ?? "field").ToLowerInvariant();
            if (!stem.EndsWith("s")) stem += "s";
            int highest = 0;
            foreach (var f in Fields)
                if (f.Name.StartsWith(stem + "-", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(f.Name.Substring(stem.Length + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
                    highest = Math.Max(highest, n);
            return $"{stem}-{highest + 1}";
        }
    }
}
