// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Manages persistent storage of slider gallery entries and folders.
    /// Sliders are saved to a JSON file in the osu! data directory.
    /// </summary>
    public class SliderGalleryStorage
    {
        private const int current_version = 2;
        private const string gallery_filename = "slider_gallery.json";

        /// <summary>
        /// Fired when entries or folders are added, removed, renamed, or moved.
        /// </summary>
        public event Action? EntriesChanged;

        private readonly Storage storage;
        private SliderGalleryData data;

        public SliderGalleryStorage(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory("slider-gallery");
            data = loadData();

            if (migrateData())
                saveData();
        }

        #region Entry operations

        /// <summary>
        /// Returns all ungrouped (root-level) gallery entries in user-defined order.
        /// </summary>
        public IReadOnlyList<SliderGalleryEntry> GetAll() => data.Entries.ToList();

        /// <summary>
        /// Returns all entries in a specific folder in user-defined order.
        /// </summary>
        public IReadOnlyList<SliderGalleryEntry> GetEntriesInFolder(Guid folderId)
        {
            var folder = data.Folders.FirstOrDefault(f => f.Id == folderId);
            return folder?.Entries.ToList() ?? new List<SliderGalleryEntry>();
        }

        /// <summary>
        /// Saves a slider to the gallery with the given name, optionally in a folder.
        /// </summary>
        public SliderGalleryEntry Add(string name, Slider slider, Guid? folderId = null)
        {
            var entry = new SliderGalleryEntry
            {
                Name = name,
                ControlPoints = slider.Path.ControlPoints.Select(cp => new SerializablePathControlPoint(cp)).ToList(),
                ExpectedDistance = slider.Path.ExpectedDistance.Value,
                SliderVelocityMultiplier = slider.SliderVelocityMultiplier,
                RepeatCount = slider.RepeatCount,
            };

            if (folderId.HasValue)
            {
                var folder = data.Folders.FirstOrDefault(f => f.Id == folderId.Value);

                if (folder != null)
                {
                    entry.FolderId = folderId;
                    folder.Entries.Insert(0, entry);
                }
                else
                {
                    // Folder not found, add to root.
                    data.Entries.Insert(0, entry);
                }
            }
            else
            {
                data.Entries.Insert(0, entry);
            }

            saveData();
            EntriesChanged?.Invoke();
            return entry;
        }

        /// <summary>
        /// Removes a gallery entry by its ID, searching both root and all folders.
        /// </summary>
        public bool Remove(Guid id)
        {
            int removed = data.Entries.RemoveAll(e => e.Id == id);

            if (removed == 0)
            {
                foreach (var folder in data.Folders)
                {
                    removed = folder.Entries.RemoveAll(e => e.Id == id);

                    if (removed > 0)
                        break;
                }
            }

            if (removed > 0)
            {
                saveData();
                EntriesChanged?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Renames a gallery entry.
        /// </summary>
        public bool Rename(Guid id, string newName)
        {
            var entry = findEntry(id);

            if (entry == null)
                return false;

            entry.Name = newName;
            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Moves an entry to a folder, or to root (ungrouped) if <paramref name="targetFolderId"/> is null.
        /// </summary>
        public bool MoveToFolder(Guid entryId, Guid? targetFolderId)
            => MoveEntry(entryId, targetFolderId, 0);

        /// <summary>
        /// Moves an entry to a folder/root at the specified index.
        /// </summary>
        public bool MoveEntry(Guid entryId, Guid? targetFolderId, int targetIndex)
        {
            var sourceEntries = findEntryList(entryId, out var entry, out int sourceIndex);

            if (entry == null)
                return false;

            var targetEntries = data.Entries;
            Guid? actualFolderId = null;

            if (targetFolderId.HasValue)
            {
                var targetFolder = data.Folders.FirstOrDefault(f => f.Id == targetFolderId.Value);

                if (targetFolder != null)
                {
                    targetEntries = targetFolder.Entries;
                    actualFolderId = targetFolder.Id;
                }
            }

            sourceEntries!.RemoveAt(sourceIndex);

            if (ReferenceEquals(sourceEntries, targetEntries) && sourceIndex < targetIndex)
                targetIndex--;

            targetIndex = Math.Clamp(targetIndex, 0, targetEntries.Count);

            entry.FolderId = actualFolderId;
            targetEntries.Insert(targetIndex, entry);

            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Creates a new <see cref="Slider"/> from a gallery entry, centered in the playfield.
        /// </summary>
        public Slider CreateSliderFromEntry(SliderGalleryEntry entry, double startTime)
        {
            var controlPoints = entry.ControlPoints.Select(cp => cp.ToPathControlPoint()).ToList();

            // Calculate the bounding box of the control points to center the slider.
            var path = new SliderPath(controlPoints.ToArray(), entry.ExpectedDistance);

            // Sample the path to find the visual bounding box.
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                var pos = path.PositionAt(t);

                minX = Math.Min(minX, pos.X);
                minY = Math.Min(minY, pos.Y);
                maxX = Math.Max(maxX, pos.X);
                maxY = Math.Max(maxY, pos.Y);
            }

            // Also include control point at origin (0,0 is the slider head position).
            minX = Math.Min(minX, 0);
            minY = Math.Min(minY, 0);
            maxX = Math.Max(maxX, 0);
            maxY = Math.Max(maxY, 0);

            var center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            var playfieldCenter = OsuPlayfield.BASE_SIZE / 2;

            // The slider's Position is the head position.
            // We want the visual center of the slider to be at the playfield center.
            var sliderPosition = playfieldCenter - center;

            var slider = new Slider
            {
                StartTime = startTime,
                Position = sliderPosition,
                RepeatCount = entry.RepeatCount,
                SliderVelocityMultiplier = entry.SliderVelocityMultiplier,
            };

            slider.Path.ControlPoints.AddRange(controlPoints);

            if (entry.ExpectedDistance.HasValue)
                slider.Path.ExpectedDistance.Value = entry.ExpectedDistance.Value;

            return slider;
        }

        #endregion

        #region Folder operations

        /// <summary>
        /// Returns all folders in user-defined order.
        /// </summary>
        public IReadOnlyList<SliderGalleryFolder> GetFolders() => data.Folders.ToList();

        /// <summary>
        /// Creates a new empty folder with the given name.
        /// </summary>
        public SliderGalleryFolder AddFolder(string name)
        {
            var folder = new SliderGalleryFolder { Name = name };
            data.Folders.Insert(0, folder);
            saveData();
            EntriesChanged?.Invoke();
            return folder;
        }

        /// <summary>
        /// Moves a folder to the specified index.
        /// </summary>
        public bool MoveFolder(Guid folderId, int targetIndex)
        {
            int sourceIndex = data.Folders.FindIndex(f => f.Id == folderId);

            if (sourceIndex < 0)
                return false;

            var folder = data.Folders[sourceIndex];
            data.Folders.RemoveAt(sourceIndex);

            if (sourceIndex < targetIndex)
                targetIndex--;

            targetIndex = Math.Clamp(targetIndex, 0, data.Folders.Count);
            data.Folders.Insert(targetIndex, folder);

            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Removes a folder by its ID. Entries in the folder are moved to ungrouped.
        /// </summary>
        public bool RemoveFolder(Guid folderId)
        {
            var folder = data.Folders.FirstOrDefault(f => f.Id == folderId);

            if (folder == null)
                return false;

            // Move all entries from the folder to root.
            foreach (var entry in folder.Entries)
                entry.FolderId = null;

            data.Entries.AddRange(folder.Entries);
            data.Folders.Remove(folder);

            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Renames a folder.
        /// </summary>
        public bool RenameFolder(Guid folderId, string newName)
        {
            var folder = data.Folders.FirstOrDefault(f => f.Id == folderId);

            if (folder == null)
                return false;

            folder.Name = newName;
            saveData();
            EntriesChanged?.Invoke();
            return true;
        }

        #endregion

        #region Persistence

        private SliderGalleryEntry? findEntry(Guid id)
        {
            var entry = data.Entries.FirstOrDefault(e => e.Id == id);

            if (entry != null)
                return entry;

            foreach (var folder in data.Folders)
            {
                entry = folder.Entries.FirstOrDefault(e => e.Id == id);

                if (entry != null)
                    return entry;
            }

            return null;
        }

        private List<SliderGalleryEntry>? findEntryList(Guid id, out SliderGalleryEntry? entry, out int index)
        {
            index = data.Entries.FindIndex(e => e.Id == id);

            if (index >= 0)
            {
                entry = data.Entries[index];
                return data.Entries;
            }

            foreach (var folder in data.Folders)
            {
                index = folder.Entries.FindIndex(e => e.Id == id);

                if (index >= 0)
                {
                    entry = folder.Entries[index];
                    return folder.Entries;
                }
            }

            entry = null;
            index = -1;
            return null;
        }

        private bool migrateData()
        {
            bool changed = false;

            if (data.Folders == null)
            {
                data.Folders = new List<SliderGalleryFolder>();
                changed = true;
            }

            if (data.Entries == null)
            {
                data.Entries = new List<SliderGalleryEntry>();
                changed = true;
            }

            foreach (var folder in data.Folders)
            {
                if (folder.Entries == null)
                {
                    folder.Entries = new List<SliderGalleryEntry>();
                    changed = true;
                }
            }

            if (data.Version < current_version)
            {
                data.Folders = data.Folders.OrderByDescending(f => f.CreatedAt).ToList();
                data.Entries = data.Entries.OrderByDescending(e => e.CreatedAt).ToList();

                foreach (var folder in data.Folders)
                    folder.Entries = folder.Entries.OrderByDescending(e => e.CreatedAt).ToList();

                data.Version = current_version;
                changed = true;
            }

            return changed;
        }

        private SliderGalleryData loadData()
        {
            try
            {
                using var stream = storage.GetStream(gallery_filename, FileAccess.Read, FileMode.OpenOrCreate);

                if (stream == null || stream.Length == 0)
                    return new SliderGalleryData();

                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();

                // Try to detect the format: if the root is an array, it's the old flat format.
                var token = JToken.Parse(json);

                if (token.Type == JTokenType.Array)
                {
                    // Migrate from old flat array format.
                    var entries = token.ToObject<List<SliderGalleryEntry>>() ?? new List<SliderGalleryEntry>();

                    Logger.Log("Migrating slider gallery from flat array to versioned format.", LoggingTarget.Runtime, LogLevel.Important);

                    var migrated = new SliderGalleryData
                    {
                        Version = 1,
                        Entries = entries,
                    };

                    return migrated;
                }

                return JsonConvert.DeserializeObject<SliderGalleryData>(json) ?? new SliderGalleryData();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to load slider gallery");
                return new SliderGalleryData();
            }
        }

        private void saveData()
        {
            try
            {
                using var stream = storage.CreateFileSafely(gallery_filename);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to save slider gallery");
            }
        }

        #endregion
    }
}
