using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>Short reasons for what a skill skipped, and the skipped things grouped by reason for its report.</summary>
    internal static class Skips
    {
        /// <summary>A few words for the report: "can't reach" rather than "no closer for 1.5 s, 0.9 m from the point".</summary>
        public static string Reason(Result result)
        {
            switch (result.Code)
            {
                case "stuck":
                case "timeout":
                case "water_in_path":
                case "too_far":
                    return "can't reach";
                case "no_space": return "no room to grow";
                case "no_build_zone": return "no-build zone";
                case "not_cultivated": return "won't stay cultivated";
                case "missed": return "can't hit it from there";
                default: return result.Detail ?? result.Code;
            }
        }

        /// <summary>A walk that didn't get there: stuck, even after jumping, or swimming.</summary>
        public static bool IsWalk(Result result) => result.Code == "stuck" || result.Code == "water_in_path";

        /// <summary>"2, 3, 5 (Rock_3 on it); 20 (can't reach)": the most common reason first, a name said twice as "Rock ×2".</summary>
        public static string Grouped<T>(IEnumerable<KeyValuePair<T, string>> skipped, System.Func<T, string> name) =>
            string.Join("; ", skipped.GroupBy(s => s.Value).OrderByDescending(g => g.Count())
                .Select(g => $"{string.Join(", ", g.GroupBy(s => name(s.Key)).Select(n => n.Count() > 1 ? $"{n.Key} ×{n.Count()}" : n.Key))} ({g.Key})"));
    }
}
