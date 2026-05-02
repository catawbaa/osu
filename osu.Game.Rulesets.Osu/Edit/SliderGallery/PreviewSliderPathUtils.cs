// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    internal static class PreviewSliderPathUtils
    {
        private const float edge_antialias_pixels = 1.5f;
        private const float minimum_antialias_portion = 0.02f;
        private const float maximum_antialias_portion = 0.35f;

        public static Color4 ApplyEdgeAntialiasing(Color4 colour, float position, float pathRadius)
        {
            float antialiasPortion = Math.Clamp(edge_antialias_pixels / Math.Max(pathRadius, 1), minimum_antialias_portion, maximum_antialias_portion);

            if (position >= antialiasPortion)
                return colour;

            colour.A *= Math.Clamp(position / antialiasPortion, 0, 1);
            return colour;
        }
    }
}
