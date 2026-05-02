// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    public partial class DefaultPreviewSliderBody : ManualSliderBody
    {
        protected override DrawableSliderPath CreateSliderPath() => new DefaultPreviewDrawableSliderPath();

        private partial class DefaultPreviewDrawableSliderPath : DrawableSliderPath
        {
            private const float opacity_at_centre = 0.3f;
            private const float opacity_at_edge = 0.8f;

            protected override Color4 ColourAt(float position)
            {
                float edgePosition = position;
                Color4 colour;

                if (CalculatedBorderPortion != 0f && position <= CalculatedBorderPortion)
                {
                    colour = BorderColour;
                }
                else
                {
                    position -= CalculatedBorderPortion;
                    colour = new Color4(AccentColour.R, AccentColour.G, AccentColour.B, (opacity_at_edge - (opacity_at_edge - opacity_at_centre) * position / GRADIENT_PORTION) * AccentColour.A);
                }

                return PreviewSliderPathUtils.ApplyEdgeAntialiasing(colour, edgePosition, PathRadius);
            }
        }
    }
}
