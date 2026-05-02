// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
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
                Color4 colour = CalculatedBorderPortion != 0f && position <= CalculatedBorderPortion
                    ? BorderColour
                    : AccentColour.Darken(4);

                return PreviewSliderPathUtils.ApplyEdgeAntialiasing(colour, position, PathRadius);
            }
        }
    }
}
