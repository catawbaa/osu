// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Utils;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A <see cref="ManualSliderBody"/> that uses the legacy slider body shader,
    /// replicating <see cref="LegacySliderBody"/>'s rendering without requiring gameplay DI.
    /// </summary>
    public partial class LegacyPreviewSliderBody : ManualSliderBody
    {
        public LegacyPreviewSliderBody()
        {
            // Legacy skins use a slightly smaller path radius than the default.
            // See OsuLegacySkinTransformer.LEGACY_CIRCLE_RADIUS.
        }

        protected override DrawableSliderPath CreateSliderPath() => new LegacyPreviewDrawableSliderPath();

        private partial class LegacyPreviewDrawableSliderPath : DrawableSliderPath
        {
            protected override Color4 ColourAt(float position)
            {
                // Replicates LegacySliderBody.LegacyDrawableSliderPath.ColourAt
                // https://github.com/peppy/osu-stable-reference/blob/3ea48705eb67172c430371dcfc8a16a002ed0d3d/osu!/Graphics/Renderers/MmSliderRendererGL.cs
                const float aa_width = 0f;

                Color4 shadow = new Color4(0, 0, 0, 0.25f);
                Color4 outerColour = AccentColour.Darken(0.1f);
                Color4 innerColour = lighten(AccentColour, 0.5f);

                const float shadow_portion = 1 - (OsuLegacySkinTransformer.LEGACY_CIRCLE_RADIUS / OsuHitObject.OBJECT_RADIUS);
                const float border_portion = 0.1875f;

                if (position <= shadow_portion - aa_width)
                    return LegacyUtils.InterpolateNonLinear(position, Color4.Black.Opacity(0f), shadow, 0, shadow_portion - aa_width);

                if (position <= shadow_portion + aa_width)
                    return LegacyUtils.InterpolateNonLinear(position, shadow, BorderColour, shadow_portion - aa_width, shadow_portion + aa_width);

                if (position <= border_portion - aa_width)
                    return BorderColour;

                if (position <= border_portion + aa_width)
                    return LegacyUtils.InterpolateNonLinear(position, BorderColour, outerColour, border_portion - aa_width, border_portion + aa_width);

                return LegacyUtils.InterpolateNonLinear(position, outerColour, innerColour, border_portion + aa_width, 1);
            }

            private static Color4 lighten(Color4 color, float amount)
            {
                amount *= 0.5f;
                return new Color4(
                    Math.Min(1, color.R * (1 + 0.5f * amount) + 1 * amount),
                    Math.Min(1, color.G * (1 + 0.5f * amount) + 1 * amount),
                    Math.Min(1, color.B * (1 + 0.5f * amount) + 1 * amount),
                    color.A);
            }
        }
    }

    /// <summary>
    /// A <see cref="ManualSliderBody"/> that uses the argon slider body shader,
    /// replicating <see cref="ArgonSliderBody"/>'s rendering without requiring gameplay DI.
    /// </summary>
    public partial class ArgonPreviewSliderBody : ManualSliderBody
    {
        protected override DrawableSliderPath CreateSliderPath() => new ArgonPreviewDrawableSliderPath();

        private partial class ArgonPreviewDrawableSliderPath : DrawableSliderPath
        {
            protected override Color4 ColourAt(float position)
            {
                // Replicates ArgonSliderBody.DrawableSliderPath.ColourAt
                if (CalculatedBorderPortion != 0f && position <= CalculatedBorderPortion)
                    return BorderColour;

                return AccentColour.Darken(4);
            }
        }
    }
}
