// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Root data model for the slider gallery JSON file.
    /// Contains folders and ungrouped (root-level) entries.
    /// </summary>
    public class SliderGalleryData
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 2;

        [JsonProperty("folders")]
        public List<SliderGalleryFolder> Folders { get; set; } = new List<SliderGalleryFolder>();

        [JsonProperty("entries")]
        public List<SliderGalleryEntry> Entries { get; set; } = new List<SliderGalleryEntry>();
    }
}
