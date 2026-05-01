// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
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
