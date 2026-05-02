// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A card representing a single slider in the gallery.
    /// Left click to place, right click for context menu (rename/delete/move).
    /// Supports drag to move between folders.
    /// </summary>
    public partial class SliderGalleryEntryCard : CompositeDrawable, IHasContextMenu, IHasPopover
    {
        public Action<SliderGalleryEntry>? OnPlace;
        public Action<SliderGalleryEntry>? OnRequestDelete;
        public Action<SliderGalleryEntry, string>? OnRequestRename;
        public Action<SliderGalleryEntry, Guid?>? OnRequestMoveToFolder;

        private readonly SliderGalleryEntry entry;

        private Box background = null!;
        private Color4 idleColour;
        private Color4 hoverColour;
        private bool isDragging;

        public SliderGalleryEntryCard(SliderGalleryEntry entry)
        {
            this.entry = entry;
        }

        internal SliderGalleryEntry Entry => entry;

        [Resolved]
        private SliderGalleryStorage galleryStorage { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            CornerRadius = 6;
            Masking = true;

            idleColour = colourProvider.Background4;
            hoverColour = colourProvider.Background3;

            // Compact mode: just the slider preview as a square thumbnail.
            RelativeSizeAxes = Axes.X;
            Width = 0.33333f;
            // Add inner padding so items don't visually touch each other in FillFlowContainer.
            Padding = new MarginPadding(2);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(3),
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = 4,
                        Masking = true,
                        MaskingSmoothness = 2f,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background6,
                            },
                            new SliderPathPreview(entry)
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        }
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!isDragging)
                background.FadeColour(hoverColour, 200, Easing.OutQuint);

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (!isDragging)
                background.FadeColour(idleColour, 200, Easing.OutQuint);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!isDragging)
                OnPlace?.Invoke(entry);

            return true;
        }

        #region Drag support

        private Drawable? dragProxy;

        protected override bool OnDragStart(DragStartEvent e)
        {
            isDragging = true;

            // Visual feedback: reduce opacity of the original in the grid.
            this.FadeTo(0.4f, 100);

            // Notify the panel that a drag is starting.
            var panel = findPanel();
            if (panel != null)
            {
                panel.DraggedEntry = entry;

                var cloneCard = new SliderGalleryEntryCard(entry);
                cloneCard.OnLoadComplete += d =>
                {
                    d.RelativeSizeAxes = Axes.X;
                    d.Width = 1f;
                };

                dragProxy = new Container
                {
                    Size = DrawSize,
                    Origin = Anchor.Centre,
                    Alpha = 0.9f,
                    Scale = new Vector2(1.05f),
                    Child = cloneCard
                };

                panel.AddDragProxy(dragProxy);
                panel.UpdateDragProxyPosition(dragProxy, e.ScreenSpaceMousePosition);
            }

            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);

            var panel = findPanel();
            if (panel != null)
            {
                panel.UpdateDragHighlight(e.ScreenSpaceMousePosition);
                if (dragProxy != null)
                    panel.UpdateDragProxyPosition(dragProxy, e.ScreenSpaceMousePosition);
            }
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            isDragging = false;

            // Animate back to normal.
            this.FadeTo(1f, 200, Easing.OutQuint);

            var panel = findPanel();

            if (panel != null)
            {
                if (dragProxy != null)
                {
                    panel.RemoveDragProxy(dragProxy);
                    dragProxy = null;
                }

                panel.HandleDrop(entry, e);
                panel.ClearDragHighlight();
                panel.DraggedEntry = null;
            }

            base.OnDragEnd(e);
        }

        protected override void Update()
        {
            base.Update();

            // Maintain a 1:1 aspect ratio based on dynamically calculated width.
            Height = DrawWidth;
        }

        private SliderGalleryPanel? findPanel()
        {
            Drawable? current = Parent;

            while (current != null)
            {
                if (current is SliderGalleryPanel panel)
                    return panel;

                current = current.Parent;
            }

            return null;
        }

        #endregion

        #region Context menu

        public MenuItem[] ContextMenuItems
        {
            get
            {
                var items = new List<MenuItem>
                {
                    new OsuMenuItem("Rename", MenuItemType.Standard, () => this.ShowPopover()),
                };

                // Build "Move to folder" submenu.
                var moveItems = new List<MenuItem>();
                var folders = galleryStorage.GetFolders();

                // Option to move to root (ungrouped).
                if (entry.FolderId != null)
                {
                    moveItems.Add(new OsuMenuItem("(root)", MenuItemType.Standard, () =>
                        OnRequestMoveToFolder?.Invoke(entry, null)));
                }

                foreach (var folder in folders)
                {
                    // Don't show the folder the entry is already in.
                    if (folder.Id == entry.FolderId)
                        continue;

                    var capturedFolder = folder;
                    moveItems.Add(new OsuMenuItem(folder.Name, MenuItemType.Standard, () =>
                        OnRequestMoveToFolder?.Invoke(entry, capturedFolder.Id)));
                }

                if (moveItems.Count > 0)
                {
                    items.Add(new OsuMenuItem("Move to folder")
                    {
                        Items = moveItems.ToArray(),
                    });
                }

                items.Add(new OsuMenuItem("Delete", MenuItemType.Destructive, () => OnRequestDelete?.Invoke(entry)));

                return items.ToArray();
            }
        }

        #endregion

        public Popover GetPopover() => new RenamePopover(entry)
        {
            OnCommit = newName => OnRequestRename?.Invoke(entry, newName),
        };

        private partial class RenamePopover : OsuPopover
        {
            public Action<string>? OnCommit;

            private readonly SliderGalleryEntry entry;
            private OsuTextBox textBox = null!;

            public RenamePopover(SliderGalleryEntry entry)
            {
                this.entry = entry;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new FillFlowContainer
                {
                    Width = 250,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Rename slider",
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                        },
                        textBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            Text = entry.Name,
                            CommitOnFocusLost = true,
                        },
                    }
                };

                textBox.OnCommit += (_, _) =>
                {
                    string newName = textBox.Text.Trim();

                    if (!string.IsNullOrEmpty(newName))
                        OnCommit?.Invoke(newName);

                    this.HidePopover();
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Schedule(() => GetContainingFocusManager()?.ChangeFocus(textBox));
            }
        }
    }
}
