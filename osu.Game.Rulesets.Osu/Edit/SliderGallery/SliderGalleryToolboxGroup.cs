// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    public partial class SliderGalleryToolboxGroup : EditorToolboxGroup
    {
        private const float default_panel_height = 300;
        private const float header_height = 30;
        private const float content_vertical_padding = 15;
        private const float toolbox_vertical_padding = 10;

        private readonly SliderGalleryPanel galleryPanel;
        private readonly List<HiddenSiblingState> hiddenSiblings = new List<HiddenSiblingState>();

        private IconButton fillToolbarButton = null!;

        private bool fillsToolbar;
        private Axes previousAutoSizeAxes;
        private float previousHeight;
        private float previousPanelHeight = default_panel_height;

        public SliderGalleryToolboxGroup()
            : base("gallery")
        {
            Child = galleryPanel = new SliderGalleryPanel();
        }

        // Reserving the extra button width makes "GALLERY" too wide for the contracted sidebar.
        protected override float AdditionalHeaderButtonsWidth => 0;

        protected override Drawable[] CreateAdditionalHeaderButtons() => new Drawable[]
        {
            fillToolbarButton = new IconButton
            {
                Icon = FontAwesome.Solid.ExpandArrowsAlt,
                Scale = new Vector2(0.75f),
                Action = toggleFillToolbar,
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            previousAutoSizeAxes = AutoSizeAxes;
            previousHeight = Height;
            previousPanelHeight = galleryPanel.Height;
        }

        protected override void Update()
        {
            base.Update();

            if (fillsToolbar)
            {
                Expanded.Value = true;
                updateFullHeight();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && fillsToolbar)
            {
                if (ThreadSafety.IsUpdateThread)
                    restoreToolbar();
                else
                    Schedule(restoreToolbar);
            }

            base.Dispose(isDisposing);
        }

        private void toggleFillToolbar()
        {
            if (fillsToolbar)
                restoreToolbar();
            else
                fillToolbar();
        }

        private void fillToolbar()
        {
            fillsToolbar = true;
            fillToolbarButton.Icon = FontAwesome.Solid.Times;

            previousAutoSizeAxes = AutoSizeAxes;
            previousHeight = Height;
            previousPanelHeight = galleryPanel.Height;

            Expanded.Value = true;

            if (Parent is FillFlowContainer parentFlow)
            {
                foreach (var sibling in parentFlow.Children)
                {
                    if (sibling == this || sibling.Alpha == 0 || sibling is not EditorToolboxGroup toolboxGroup)
                        continue;

                    hiddenSiblings.Add(new HiddenSiblingState(toolboxGroup));
                    toolboxGroup.ClearTransforms();
                    // Keep hidden groups present so their non-positional input/key bindings remain active.
                    toolboxGroup.AlwaysPresent = true;
                    toolboxGroup.Hide();
                    toolboxGroup.AutoSizeAxes = Axes.None;
                    toolboxGroup.Height = 0;
                    toolboxGroup.Margin = new MarginPadding();
                }
            }

            AutoSizeAxes = Axes.None;
            updateFullHeight();
        }

        private void restoreToolbar()
        {
            if (!fillsToolbar)
                return;

            fillsToolbar = false;
            fillToolbarButton.Icon = FontAwesome.Solid.ExpandArrowsAlt;

            Height = previousHeight;
            galleryPanel.Height = previousPanelHeight;
            AutoSizeAxes = previousAutoSizeAxes;

            foreach (var sibling in hiddenSiblings)
                sibling.Restore();

            hiddenSiblings.Clear();
        }

        private void updateFullHeight()
        {
            var toolbox = findContainingToolbox();

            if (toolbox == null || toolbox.DrawHeight <= 0)
                return;

            Height = toolbox.DrawHeight - toolbox_vertical_padding;
            galleryPanel.Height = Height - header_height - content_vertical_padding;
        }

        private ExpandingToolboxContainer? findContainingToolbox()
        {
            Drawable? current = Parent;

            while (current != null)
            {
                if (current is ExpandingToolboxContainer toolbox)
                    return toolbox;

                current = current.Parent;
            }

            return null;
        }

        private readonly struct HiddenSiblingState
        {
            private readonly EditorToolboxGroup drawable;
            private readonly Axes autoSizeAxes;
            private readonly float height;
            private readonly float alpha;
            private readonly MarginPadding margin;
            private readonly bool alwaysPresent;

            public HiddenSiblingState(EditorToolboxGroup drawable)
            {
                this.drawable = drawable;
                autoSizeAxes = drawable.AutoSizeAxes;
                height = drawable.Height;
                alpha = drawable.Alpha;
                margin = drawable.Margin;
                alwaysPresent = drawable.AlwaysPresent;
            }

            public void Restore()
            {
                drawable.ClearTransforms();
                drawable.Height = height;
                drawable.Margin = margin;
                drawable.Alpha = alpha;
                drawable.AutoSizeAxes = autoSizeAxes;
                drawable.AlwaysPresent = alwaysPresent;
            }
        }
    }
}
