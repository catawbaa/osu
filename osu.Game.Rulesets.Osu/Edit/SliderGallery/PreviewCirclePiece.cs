// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Skinning;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Identifies the detected skin type for slider preview rendering.
    /// </summary>
    public enum PreviewSkinType
    {
        Default,
        Legacy,
        Argon,
    }

    /// <summary>
    /// A simplified hit circle piece for slider gallery previews that renders
    /// skin-appropriate visuals without requiring <c>DrawableHitObject</c> DI.
    /// </summary>
    public partial class PreviewCirclePiece : CompositeDrawable
    {
        private readonly PreviewSkinType skinType;
        private readonly Color4 accentColour;
        private readonly bool isHead;

        /// <summary>
        /// Creates a preview circle piece.
        /// </summary>
        /// <param name="skinType">The detected skin type.</param>
        /// <param name="accentColour">The accent colour for tinting.</param>
        /// <param name="isHead">True for slider head, false for slider tail.</param>
        public PreviewCirclePiece(PreviewSkinType skinType, Color4 accentColour, bool isHead)
        {
            this.skinType = skinType;
            this.accentColour = accentColour;
            this.isHead = isHead;

            Size = OsuHitObject.OBJECT_DIMENSIONS;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            switch (skinType)
            {
                case PreviewSkinType.Legacy:
                    loadLegacyPiece(skin);
                    break;

                case PreviewSkinType.Argon:
                    loadArgonPiece();
                    break;

                default:
                    loadDefaultPiece();
                    break;
            }

            if (isHead)
            {
                AddInternal(new SkinnableSpriteText(new OsuSkinComponentLookup(OsuSkinComponents.HitCircleText), _ => new OsuSpriteText
                {
                    Font = OsuFont.Numeric.With(size: 40),
                    UseFullGlyphHeight = false,
                }, confineMode: ConfineMode.NoScaling)
                {
                    Text = "1",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }
        }

        private void loadLegacyPiece(ISkinSource skin)
        {
            string prefix = isHead ? "sliderstartcircle" : "sliderendcircle";
            const string fallback = "hitcircle";

            Vector2 maxSize = OsuHitObject.OBJECT_DIMENSIONS * 2;

            // Check if the skin has the specific prefix texture, otherwise fall back
            var provider = skin.FindProvider(s => s.GetTexture(fallback) != null) ?? skin;
            string circleName = (provider.GetTexture(prefix) != null) ? prefix : fallback;

            var circleTexture = skin.GetTexture(circleName)?.WithMaximumSize(maxSize);
            var overlayTexture = skin.GetTexture($"{circleName}overlay")?.WithMaximumSize(maxSize);

            if (circleTexture != null)
            {
                AddInternal(new Sprite
                {
                    Texture = circleTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = LegacyColourCompatibility.DisallowZeroAlpha(accentColour),
                });
            }

            if (overlayTexture != null)
            {
                AddInternal(new Sprite
                {
                    Texture = overlayTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }
        }

        private void loadArgonPiece()
        {
            // Simplified version of ArgonMainCirclePiece without number, animation, or DI dependencies.
            var circleSize = OsuHitObject.OBJECT_DIMENSIONS;

            InternalChildren = new Drawable[]
            {
                new Circle
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = circleSize - new Vector2(1),
                    Colour = accentColour.Darken(4),
                    // Outer fill only shown for standalone hit circles (head=true uses withOuterFill=false in argon)
                    Alpha = 0,
                },
                new Circle // outer gradient
                {
                    Size = new Vector2(ArgonMainCirclePiece.OUTER_GRADIENT_SIZE),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = ColourInfo.GradientVertical(accentColour, accentColour.Darken(0.1f)),
                },
                new Circle // inner gradient
                {
                    Size = new Vector2(ArgonMainCirclePiece.INNER_GRADIENT_SIZE),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = ColourInfo.GradientVertical(accentColour.Darken(0.5f), accentColour.Darken(0.6f)),
                },
                new Circle // inner fill
                {
                    Size = new Vector2(ArgonMainCirclePiece.INNER_FILL_SIZE),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = accentColour.Darken(4),
                },
                new RingPiece(ArgonMainCirclePiece.BORDER_THICKNESS),
            };
        }

        private void loadDefaultPiece()
        {
            // Simplified version of MainCirclePiece — a coloured circle with a ring.
            InternalChildren = new Drawable[]
            {
                new CirclePiece
                {
                    Colour = accentColour,
                },
                new RingPiece(),
            };
        }
    }
}
