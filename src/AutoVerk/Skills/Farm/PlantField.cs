using System;
using System.Collections.Generic;
using System.Linq;
using AutoVerk.Engine;
using AutoVerk.Game;
using AutoVerk.Skills.Basic;
using UnityEngine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>
    /// Plant a field with the Cultivator: a new one laid out in front of you (and saved), or a saved one to fill
    /// up. Spot by spot, nearest first: pick up a stone or branch on it, cultivate, sow. Rests when stamina runs
    /// low. A spot the game keeps refusing, or with something in the way, is skipped and named in the report.
    /// Not named Plant: the game has a global class of that name.
    /// </summary>
    public sealed class PlantField : Composite
    {
        private const int MaxTries = 3;
        /// <summary>Stand this far behind a spot along the path, on the side already worked, so the body never covers it.</summary>
        private const float StandBack = 1f;
        /// <summary>The game says no for a reason about the spot itself: one refusal is enough.</summary>
        private static readonly HashSet<string> SpotRefusals = new HashSet<string> { "no_space", "no_build_zone" };

        private readonly string _seed;
        private readonly string _fieldName;
        private readonly int _count;
        private readonly int _line;
        private readonly bool _staggered;
        private readonly Dictionary<int, string> _skipped = new Dictionary<int, string>();
        private readonly Dictionary<int, int> _tries = new Dictionary<int, int>();

        /// <param name="fieldName">A saved field to fill up (it keeps its own seed), or null for a new one here.</param>
        public PlantField(string seed, string fieldName = null, int count = 20, int line = 10, bool staggered = true)
        {
            _seed = seed;
            _fieldName = string.IsNullOrEmpty(fieldName) ? null : fieldName;
            _count = count;
            _line = line;
            _staggered = staggered;
        }

        protected override IEnumerable<Step> Run()
        {
            var book = Fields.Load(out var error);
            if (book == null) yield return Fail("refused", error);
            var field = _fieldName != null ? book.Find(_fieldName) : null;
            if (_fieldName != null && field == null) yield return Fail("not_found", $"no field {_fieldName} in this world");
            var seed = field?.Seed ?? _seed;
            var plant = PlantData.ForSeed(seed);
            if (plant == null) yield return Fail("not_found", string.IsNullOrEmpty(seed) ? "choose a seed" : $"nothing plantable uses {seed}");
            var cultivator = SpotProbe.FindCultivator();
            if (cultivator == null) yield return Fail("no_tool", "no Cultivator in the inventory");
            if (Senses.Count(seed) == 0) yield return Fail("no_materials", $"no {seed} in the inventory");
            if (field == null)
            {
                field = Fields.LayOut(book.NextName(plant.Crop), plant, _count, Math.Min(_line, _count), _staggered);
                error = field.Validate() ?? Save(book, field);
                if (error != null) yield return Fail("refused", error);
            }

            var spots = field.Spots();
            int planted = 0;
            bool outOfSeeds = false;
            // A spot takes a pick, a cultivate and a sow, each retried a few times: never more than that.
            for (int pass = 0; pass < field.Count * 3 * MaxTries; pass++)
            {
                // Look: the nearest spot that still needs work, read afresh each pass.
                var next = Nearest(field, spots, plant, out var state, out var on, out var pos);
                if (next == null) break;
                var spot = next.Value;
                Status = $"{field.Name}: spot {spot.Index + 1}/{field.Count}, {planted} planted";

                if (state == SpotState.Loose)
                {
                    yield return Try(new MoveTo(on));
                    if (Last.Ok) yield return Try(new Interact(on));
                    if (!Last.Ok) Refused(spot, Last);
                    continue;
                }
                if (Senses.Count(seed) == 0)
                {
                    outOfSeeds = true;
                    break;
                }

                yield return Do(new Equip(cultivator));
                if (!Senses.Player.HaveStamina(cultivator.m_shared.m_attack.m_attackStamina)) yield return Try(new Rest());
                var stand = pos - new Vector3((float)spot.PathX, 0f, (float)spot.PathZ) * StandBack;
                if (Senses.FlatDistance(Senses.Player.transform.position, stand) > 0.5f)
                {
                    yield return Try(new MoveTo(stand, stopAt: 0.35f));
                    if (!Last.Ok)
                    {
                        Refused(spot, Last);
                        continue;
                    }
                }

                var table = Senses.BuildPieces();
                var piece = state == SpotState.Empty ? SpotProbe.CultivatePiece(table) : SpotProbe.SaplingPiece(table, plant);
                if (piece == null) yield return Fail("no_tool", $"the {Senses.ItemId(cultivator)} can't plant {seed}");
                yield return Try(new Place(piece, pos));
                if (Last.Ok)
                {
                    if (state == SpotState.Cultivated) planted++;
                    // Counted, in case the ground doesn't stay cultivated and the spot comes up again.
                    else Refused(spot, Result.Fail("not_cultivated", "the ground won't stay cultivated"));
                }
                else if (Last.Code == "no_materials" && state == SpotState.Cultivated)
                {
                    outOfSeeds = true;
                    break;
                }
                else if (Last.Code == "no_stamina") yield return Try(new Rest());
                else Refused(spot, Last);
            }

            Report = $"{planted} planted on {field.Name}"
                     + (_skipped.Count > 0 ? $"; skipped {string.Join(", ", _skipped.Select(s => $"spot {s.Key + 1} ({s.Value})"))}" : "")
                     + (outOfSeeds ? $"; out of {seed}" : "");
        }

        /// <summary>The nearest spot not planted and not skipped, with what's on it; null when the field is done.</summary>
        private Spot? Nearest(Field field, List<Spot> spots, PlantData plant, out SpotState state, out Component on, out Vector3 pos)
        {
            Spot? best = null;
            float bestDistance = float.MaxValue;
            state = SpotState.Empty;
            on = null;
            pos = Vector3.zero;
            var from = Senses.Player.transform.position;
            foreach (var spot in spots)
            {
                if (_skipped.ContainsKey(spot.Index)) continue;
                var at = Fields.Ground(field, spot);
                var s = SpotProbe.Probe(at, (float)field.Spacing, plant, out var what, out var name);
                if (s == SpotState.Planted) continue;
                if (s == SpotState.Taken || s == SpotState.Blocked)
                {
                    _skipped[spot.Index] = $"{name} on it";
                    continue;
                }
                float d = Senses.FlatDistance(from, at);
                if (d >= bestDistance) continue;
                best = spot;
                bestDistance = d;
                state = s;
                on = what;
                pos = at;
            }
            return best;
        }

        /// <summary>A refusal about the spot skips it at once; others, and repeated cultivating, get a few tries.</summary>
        private void Refused(Spot spot, Result result)
        {
            _tries.TryGetValue(spot.Index, out var n);
            _tries[spot.Index] = ++n;
            if (SpotRefusals.Contains(result.Code) || n >= MaxTries) _skipped[spot.Index] = result.Detail ?? result.Code;
        }

        private static string Save(FieldBook book, Field field)
        {
            book.Fields.Add(field);
            try
            {
                Fields.Save(book);
                return null;
            }
            catch (Exception e)
            {
                return $"can't save the field: {e.Message}";
            }
        }

        public override string ToString() => _fieldName != null ? $"farm.plant {_fieldName}" : $"farm.plant {_seed}";
    }
}
