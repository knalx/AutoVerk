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
    /// up. First, if asked, break the rocks that would stop the plants growing. Then spot by spot, nearest first:
    /// pick up a stone or branch on it (if asked), cultivate, sow. Rests when stamina runs low. A spot the game
    /// keeps refusing, or with something in the way, is skipped and named in the report.
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
        private readonly bool _pickStones;
        private readonly bool _mineRocks;
        private readonly Dictionary<int, string> _skipped = new Dictionary<int, string>();
        private readonly Dictionary<int, int> _tries = new Dictionary<int, int>();

        /// <param name="fieldName">A saved field to fill up (it keeps its own seed), or null for a new one here.</param>
        /// <param name="pickStones">Pick up a stone or branch on a spot; without it, the spot is skipped.</param>
        /// <param name="mineRocks">Break the rocks on and around the spots with a pickaxe first.</param>
        public PlantField(string seed, string fieldName = null, int count = 20, int line = 10, bool staggered = true,
            bool pickStones = true, bool mineRocks = false)
        {
            _seed = seed;
            _fieldName = string.IsNullOrEmpty(fieldName) ? null : fieldName;
            _count = count;
            _line = line;
            _staggered = staggered;
            _pickStones = pickStones;
            _mineRocks = mineRocks;
        }

        protected override IEnumerable<Step> Run()
        {
            var field = Choose(out var book, out var plant, out var failure);
            if (field == null) yield return Fail(failure.Code, failure.Detail);
            var seed = field.Seed;
            var cultivator = SpotProbe.FindCultivator();
            if (cultivator == null) yield return Fail("no_tool", "no Cultivator in the inventory");
            if (Senses.Count(seed) == 0) yield return Fail("no_materials", $"no {seed} in the inventory");
            if (!book.Fields.Contains(field))
            {
                var error = Save(book, field);
                if (error != null) yield return Fail("refused", error);
            }

            var spots = field.Spots();
            string rocks = null;
            if (_mineRocks)
            {
                // Before any sowing: a pickaxe swing can destroy saplings nearby.
                var clear = new ClearRocks(spots.Select(s => Fields.Ground(field, s)), RockRadius(plant));
                yield return Try(clear);
                rocks = clear.Report ?? Last.ToString();
            }

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
                    if (!_pickStones)
                    {
                        _skipped[spot.Index] = $"{Utils.GetPrefabName(on.gameObject)} on it";
                        continue;
                    }
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
                     + (outOfSeeds ? $"; out of {seed}" : "")
                     + (rocks != null ? $"; {rocks}" : "");
        }

        /// <summary>
        /// Poles on the field's spots, nothing done: green ready, yellow a stone or rock to clear first, red to be
        /// skipped, blue planted already. A new field is laid out from where you stand but not saved.
        /// </summary>
        public string Preview()
        {
            Markers.Hide();
            var field = Choose(out var book, out var plant, out var failure);
            if (field == null) return failure.Detail;
            float radius = RockRadius(plant);
            int free = 0, clear = 0, blocked = 0, done = 0;
            foreach (var spot in field.Spots())
            {
                var at = Fields.Ground(field, spot);
                var state = SpotProbe.Probe(at, (float)field.Spacing, plant, out _, out _);
                bool rock = (state == SpotState.Empty || state == SpotState.Cultivated || state == SpotState.Blocked) &&
                            Rocks.Find(at, radius, out _) != null;
                Color color;
                if (state == SpotState.Planted) { color = Markers.Done; done++; }
                else if (rock) { color = _mineRocks ? Markers.Clear : Markers.Blocked; if (_mineRocks) clear++; else blocked++; }
                else if (state == SpotState.Loose && _pickStones) { color = Markers.Clear; clear++; }
                else if (state == SpotState.Empty || state == SpotState.Cultivated) { color = Markers.Free; free++; }
                else { color = Markers.Blocked; blocked++; }
                Markers.Pole(at, color, $"spot {spot.Index + 1}");
            }
            var name = book.Fields.Contains(field) ? field.Name : $"{field.Name} (new, not saved)";
            return $"{name}: {free} ready (green), {clear} to clear first (yellow), {blocked} to skip (red)" +
                   (done > 0 ? $", {done} planted (blue)" : "");
        }

        /// <summary>
        /// The field to work and what grows there: the saved one asked for, or a new one laid out in front of the
        /// player, not saved yet. Null and why not when there's none.
        /// </summary>
        private Field Choose(out FieldBook book, out PlantData plant, out Result failure)
        {
            plant = null;
            failure = null;
            book = Fields.Load(out var error);
            if (book == null)
            {
                failure = Result.Fail("refused", error);
                return null;
            }
            var field = _fieldName != null ? book.Find(_fieldName) : null;
            if (_fieldName != null && field == null)
            {
                failure = Result.Fail("not_found", $"no field {_fieldName} in this world");
                return null;
            }
            var seed = field?.Seed ?? _seed;
            plant = PlantData.ForSeed(seed);
            if (plant == null)
            {
                failure = Result.Fail("not_found", string.IsNullOrEmpty(seed) ? "choose a seed" : $"nothing plantable uses {seed}");
                return null;
            }
            if (field != null) return field;
            field = Fields.LayOut(book.NextName(plant.Crop), plant, _count, Math.Min(_line, _count), _staggered);
            error = field.Validate();
            if (error == null) return field;
            failure = Result.Fail("refused", error);
            return null;
        }

        /// <summary>How far from a spot a rock stops the plant: the grow radius the game checks, or right on the spot.</summary>
        private static float RockRadius(PlantData plant) => Mathf.Max(plant.GrowRadius, 0.4f);

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

        public override string ToString() => _fieldName != null ? $"plant {_fieldName}" : $"plant {_seed}";
    }
}
