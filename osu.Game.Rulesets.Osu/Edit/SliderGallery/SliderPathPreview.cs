// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A small drawable that renders a slider path using skin-appropriate visuals,
    /// with head and tail circles, auto-scaled to fit within a given bounding box.
    /// Detects the active skin (Legacy, Argon, or Default/Triangles) and renders
    /// the slider body and endpoint circles accordingly.
    /// </summary>
    /// <remarks>
    /// To ensure proper antialiasing at thumbnail scale, the path vertices and radius
    /// are pre-scaled to the final display size rather than rendering at full game
    /// resolution and using container scaling. This keeps <c>SmoothPath</c>'s edge AA
    /// operating at the correct pixel density.
    /// </remarks>
    public partial class SliderPathPreview : CompositeDrawable
    {
        private static readonly Color4 accent_colour = Color4Extensions.FromHex("#4CB290");

        private const float cs_scale = 0.5f;
        private const float minimum_body_width = 20;

        private readonly SliderGalleryEntry entry;

        // Raw (unscaled) data computed in load, applied at display scale in Update.
        private IReadOnlyList<Vector2>? rawCalculatedPath;
        private Vector2 rawPathSize;
        private Vector2 rawTailPos;
        private float rawPathRadius;
        private PreviewSkinType skinType;

        private Container contentContainer = null!;
        private ManualSliderBody body = null!;
        private PreviewCirclePiece headCircle = null!;
        private PreviewCirclePiece? tailCircle;

        // Captured once from the unscaled body layout; reused on subsequent resizes.
        private Vector2 rawContentSize;
        // The draw size at which layout was last applied; triggers re-layout when it changes.
        private Vector2 lastAppliedDrawSize;

        private Box contractedOverlay = null!;

        public SliderPathPreview(SliderGalleryEntry entry)
        {
            this.entry = entry;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var controlPoints = entry.ControlPoints.Select(cp => cp.ToPathControlPoint()).ToArray();
            var sliderPath = new SliderPath(controlPoints, entry.ExpectedDistance);
            var calculatedPath = sliderPath.CalculatedPath;

            if (calculatedPath.Count == 0)
                return;

            skinType = detectSkinType(skin);
            rawCalculatedPath = calculatedPath.ToList();
            rawPathSize = new Vector2(
                rawCalculatedPath.Max(v => v.X) - rawCalculatedPath.Min(v => v.X),
                rawCalculatedPath.Max(v => v.Y) - rawCalculatedPath.Min(v => v.Y));
            rawTailPos = sliderPath.PositionAt(1);

            body = createBody(skinType, skin, out rawPathRadius);
            // Set initial vertices so the body can auto-size for bounding box calculation.
            body.SetVertices(calculatedPath);

            headCircle = new PreviewCirclePiece(skinType, accent_colour, isHead: true)
            {
                Scale = new Vector2(cs_scale),
            };

            var children = new Drawable[] { body, headCircle };

            // Legacy skins show a visible tail circle (sliderendcircle).
            // Argon and Default skins don't render a tail circle.
            if (skinType == PreviewSkinType.Legacy)
            {
                tailCircle = new PreviewCirclePiece(skinType, accent_colour, isHead: false)
                {
                    Alpha = 0.8f,
                    Scale = new Vector2(cs_scale),
                };
                children = new Drawable[] { body, tailCircle, headCircle };
            }

            InternalChildren = new Drawable[]
            {
                contentContainer = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Children = children,
                },
                contractedOverlay = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = accent_colour,
                    Alpha = 0,
                },
            };

        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Walk up the tree to find the ExpandingToolboxContainer and bind to its
            // Expanded bindable, which reflects the actual visual state (including hover
            // expansion) — unlike the raw EditorContractSidebars setting.
            Drawable? current = Parent;

            while (current != null)
            {
                if (current is ExpandingToolboxContainer toolbox)
                {
                    toolbox.Expanded.BindValueChanged(e =>
                    {
                        if (e.NewValue)
                            contractedOverlay.FadeOut(200, Easing.OutQuint);
                        else
                            contractedOverlay.FadeIn(200, Easing.OutQuint);
                    }, true);
                    break;
                }

                current = current.Parent;
            }
        }

        /// <summary>
        /// Detects the active skin type by probing the skin source for known components.
        /// </summary>
        private static PreviewSkinType detectSkinType(ISkinSource skin)
        {
            // Try to create a slider body component — check its type without loading it.
            var testDrawable = skin.GetDrawableComponent(new OsuSkinComponentLookup(OsuSkinComponents.SliderBody));

            if (testDrawable is LegacySliderBody)
                return PreviewSkinType.Legacy;

            if (testDrawable is ArgonSliderBody)
                return PreviewSkinType.Argon;

            return PreviewSkinType.Default;
        }

        /// <summary>
        /// Creates the appropriate <see cref="ManualSliderBody"/> for the detected skin.
        /// The <paramref name="pathRadius"/> output is the unscaled path radius used for scale calculations.
        /// </summary>
        private static ManualSliderBody createBody(PreviewSkinType skinType, ISkinSource skin, out float pathRadius)
        {
            switch (skinType)
            {
                case PreviewSkinType.Legacy:
                {
                    pathRadius = OsuHitObject.OBJECT_RADIUS * cs_scale;
                    return new LegacyPreviewSliderBody
                    {
                        PathRadius = pathRadius,
                        AccentColour = (skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderTrackOverride)?.Value ?? accent_colour).Opacity(0.7f),
                        BorderColour = skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderBorder)?.Value ?? Color4.White,
                    };
                }

                case PreviewSkinType.Argon:
                {
                    pathRadius = (ArgonMainCirclePiece.OUTER_GRADIENT_SIZE / 2) * cs_scale;
                    float intendedThickness = ArgonMainCirclePiece.GRADIENT_THICKNESS / pathRadius;
                    float borderSize = intendedThickness / DrawableSliderPath.BORDER_PORTION;

                    return new ArgonPreviewSliderBody
                    {
                        PathRadius = pathRadius,
                        AccentColour = accent_colour,
                        BorderColour = accent_colour,
                        BorderSize = borderSize,
                    };
                }

                default:
                {
                    pathRadius = OsuHitObject.OBJECT_RADIUS * cs_scale;
                    return new DefaultPreviewSliderBody
                    {
                        PathRadius = pathRadius,
                        AccentColour = accent_colour,
                    };
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (rawCalculatedPath == null || contentContainer == null || DrawWidth <= 0 || DrawHeight <= 0)
                return;

            // Capture the unscaled content bounding box the very first time layout is available.
            // We store it so subsequent resize recalculations always use the original unscaled size.
            if (rawContentSize == Vector2.Zero)
            {
                float rawW = contentContainer.DrawWidth;
                float rawH = contentContainer.DrawHeight;

                if (rawW <= 0 || rawH <= 0)
                    return;

                rawContentSize = new Vector2(rawW, rawH);
            }

            // Only re-apply layout when the available draw size has actually changed
            // (e.g. toolbar expand/contract, folder open/close resizing the grid).
            var currentDrawSize = new Vector2(DrawWidth, DrawHeight);

            if (lastAppliedDrawSize == currentDrawSize)
                return;

            lastAppliedDrawSize = currentDrawSize;

            float padding = 4;
            float availableWidth = DrawWidth - padding * 2;
            float availableHeight = DrawHeight - padding * 2;
            float scale = Math.Min(availableWidth / rawContentSize.X, availableHeight / rawContentSize.Y);
            float pathRadius = rawPathRadius * scale;

            if (pathRadius * 2 < minimum_body_width)
            {
                float scaleWithMinimumBodyWidth = Math.Min(
                    rawPathSize.X > 0 ? Math.Max(0, (availableWidth - minimum_body_width) / rawPathSize.X) : scale,
                    rawPathSize.Y > 0 ? Math.Max(0, (availableHeight - minimum_body_width) / rawPathSize.Y) : scale);

                scale = Math.Min(scale, scaleWithMinimumBodyWidth);
                pathRadius = minimum_body_width / 2;
            }

            // Re-set the path at the final display scale so that SmoothPath's edge
            // antialiasing (which operates at a fixed pixel width) works correctly
            // at thumbnail size, rather than being rendered at full game resolution
            // and then crushed down via container scaling.
            body.PathRadius = pathRadius;
            body.SetVertices(rawCalculatedPath.Select(v => v * scale).ToList());

            // Position circles relative to the body's (now-scaled) coordinate system.
            var pathOffset = body.PathOffset;
            float circleScale = pathRadius * cs_scale / rawPathRadius;
            headCircle.Position = pathOffset;
            headCircle.Scale = new Vector2(circleScale);

            if (tailCircle != null)
            {
                tailCircle.Position = pathOffset + rawTailPos * scale;
                tailCircle.Scale = new Vector2(circleScale);
            }
        }
    }
}
