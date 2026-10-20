using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoVerk.Game;
using UnityEngine;

namespace AutoVerk.Skills.Farm
{
    /// <summary>The loaded world's fields, in BepInEx/config/AutoVerk/&lt;world&gt;.fields.txt, and where their spots are.</summary>
    public static class Fields
    {
        private static string Folder => Path.Combine(BepInEx.Paths.ConfigPath, "AutoVerk");

        private static string WorldName => ZNet.instance != null ? ZNet.instance.GetWorldName() : null;

        private static string PathFor(string world) =>
            Path.Combine(Folder, new string(world.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray()) + ".fields.txt");

        /// <summary>The world's fields: empty with no file yet, or null and why when the file can't be read.</summary>
        public static FieldBook Load(out string error)
        {
            error = null;
            var world = WorldName;
            if (world == null)
            {
                error = "not in a world";
                return null;
            }
            var path = PathFor(world);
            try
            {
                return File.Exists(path) ? FieldBook.FromText(File.ReadAllText(path)) : new FieldBook();
            }
            catch (Exception e)
            {
                error = $"can't read {path}: {e.Message}";
                return null;
            }
        }

        /// <summary>Writes the whole book through a temporary file, so a crash never leaves half a file.</summary>
        public static void Save(FieldBook book)
        {
            var world = WorldName ?? throw new InvalidOperationException("not in a world");
            Directory.CreateDirectory(Folder);
            var path = PathFor(world);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, book.ToText());
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        /// <summary>Forgets a saved field; its plants stay in the world. What happened, for the window.</summary>
        public static string Delete(string name)
        {
            var book = Load(out var error);
            if (book == null) return error;
            var field = book.Find(name);
            if (field == null) return $"no field {name} in this world";
            book.Fields.Remove(field);
            Save(book);
            return $"Deleted the field {name}; its plants stay.";
        }

        /// <summary>The names of the world's saved fields, for the window.</summary>
        public static IEnumerable<string> Names() => Load(out _)?.Fields.Select(f => f.Name) ?? Enumerable.Empty<string>();

        /// <summary>A new field in front of the player, facing where the player faces.</summary>
        public static Field LayOut(string name, PlantData plant, int count, int lineLength, bool staggered)
        {
            var p = Senses.Player.transform;
            var forward = p.forward;
            forward.y = 0f;
            forward.Normalize();
            return new Field
            {
                Name = name,
                Seed = plant.Seed,
                X = p.position.x, Y = p.position.y, Z = p.position.z,
                ForwardX = forward.x, ForwardZ = forward.z,
                Count = count,
                LineLength = lineLength,
                Spacing = plant.MinSpacing,
                Staggered = staggered,
            };
        }

        /// <summary>A spot on the ground.</summary>
        public static Vector3 Ground(Field field, Spot spot)
        {
            var p = new Vector3((float)spot.X, (float)field.Y, (float)spot.Z);
            if (ZoneSystem.instance != null) p.y = ZoneSystem.instance.GetGroundHeight(p);
            return p;
        }
    }
}
