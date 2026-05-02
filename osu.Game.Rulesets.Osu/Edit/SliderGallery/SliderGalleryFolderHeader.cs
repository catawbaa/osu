// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A collapsible header for a folder in the slider gallery panel.
    /// Displays the folder name, entry count, and a chevron indicator.
    /// </summary>
    public partial class SliderGalleryFolderHeader : CompositeDrawable, IHasContextMenu
    {
        public Action<SliderGalleryFolder>? OnRequestDelete;
        public Action<SliderGalleryFolder, string>? OnRequestRename;
        public Action<SliderGalleryFolder>? OnToggleExpanded;

        /// <summary>
        /// Whether this folder is a valid drop target (entry being dragged over it).
        /// </summary>
        public bool IsDropTarget
        {
            set
            {
                if (background == null) return;

                if (value)
                {
                    background.FadeColour(dropTargetColour, 100);
                    this.ScaleTo(1.02f, 150, Easing.OutQuint);
                }
                else
                {
                    background.FadeColour(IsHovered ? hoverColour : idleColour, 200, Easing.OutQuint);
                    this.ScaleTo(1f, 200, Easing.OutQuint);
                }
            }
        }

        private readonly SliderGalleryFolder folder;
        public Guid FolderId => folder.Id;
        private readonly bool expanded;
        private readonly int entryCount;

        private Box background = null!;
        private SpriteIcon chevron = null!;
        private Color4 idleColour;
        private Color4 hoverColour;
        private Color4 dropTargetColour;
        private TruncatingSpriteText folderNameText = null!;
        private OsuTextBox folderTextBox = null!;
        private bool suppressClick;
        private Drawable? dragProxy;

        [Resolved(canBeNull: true)]
        private IExpandingContainer? expandingContainer { get; set; }

        public SliderGalleryFolderHeader(SliderGalleryFolder folder, bool expanded, int entryCount)
        {
            this.folder = folder;
            this.expanded = expanded;
            this.entryCount = entryCount;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuColour colours)
        {
            RelativeSizeAxes = Axes.X;
            Height = 28;

            idleColour = colourProvider.Background3;
            hoverColour = colourProvider.Background2;
            dropTargetColour = colourProvider.Highlight1.Opacity(0.5f);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 8 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                                Margin = new MarginPadding { Right = 6 },
                                Children = new Drawable[]
                                {
                                    chevron = new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(10),
                                        Icon = expanded ? FontAwesome.Solid.ChevronDown : FontAwesome.Solid.ChevronRight,
                                        Colour = colourProvider.Light4,
                                    },
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(12),
                                        Icon = FontAwesome.Solid.Folder,
                                        Colour = colourProvider.Light3,
                                    },
                                }
                            },
                            new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Right = 10 },
                                Children = new Drawable[]
                                {
                                    folderNameText = new TruncatingSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = folder.Name,
                                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                        RelativeSizeAxes = Axes.X,
                                    },
                                    folderTextBox = new FolderNameTextBox
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = folder.Name,
                                        RelativeSizeAxes = Axes.X,
                                        Height = 20,
                                        Alpha = 0,
                                        CommitOnFocusLost = true,
                                        Margin = new MarginPadding { Left = -4 },
                                    }
                                }
                            },
                            new CircularContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colours.Orange1,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Text = entryCount.ToString(),
                                        Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                                        Colour = Color4.Black,
                                        Margin = new MarginPadding { Horizontal = 6, Vertical = 1 },
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            expandingContainer?.Expanded.BindValueChanged(containerExpanded =>
            {
                if (folderTextBox.Alpha == 0)
                    folderNameText.FadeTo(containerExpanded.NewValue ? 1 : 0, 200, Easing.OutQuint);
            }, true);

            folderTextBox.OnCommit += (sender, newText) =>
            {
                folderTextBox.Alpha = 0;
                folderNameText.ClearTransforms();
                folderNameText.Alpha = 1;

                if (string.IsNullOrWhiteSpace(sender.Text) || sender.Text == folder.Name)
                {
                    folderNameText.Text = folder.Name;
                    return;
                }

                OnRequestRename?.Invoke(folder, sender.Text);
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(hoverColour, 200, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(idleColour, 200, Easing.OutQuint);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (suppressClick)
            {
                suppressClick = false;
                return true;
            }

            OnToggleExpanded?.Invoke(folder);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (folder.Id == Guid.Empty)
                return false;

            suppressClick = true;
            this.FadeTo(0.4f, 100);

            var panel = findPanel();

            if (panel != null)
            {
                panel.DraggedFolder = folder;

                var cloneHeader = new SliderGalleryFolderHeader(folder, expanded, entryCount);
                cloneHeader.OnLoadComplete += d =>
                {
                    d.RelativeSizeAxes = Axes.X;
                    d.Width = 1f;
                };

                dragProxy = new Container
                {
                    Size = DrawSize,
                    Origin = Anchor.Centre,
                    Alpha = 0.9f,
                    Child = cloneHeader
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
            this.FadeTo(1f, 200, Easing.OutQuint);

            var panel = findPanel();

            if (panel != null)
            {
                if (dragProxy != null)
                {
                    panel.RemoveDragProxy(dragProxy);
                    dragProxy = null;
                }

                panel.HandleFolderDrop(folder, e);
                panel.ClearDragHighlight();
                panel.DraggedFolder = null;
            }

            Schedule(() => suppressClick = false);

            base.OnDragEnd(e);
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

        public MenuItem[] ContextMenuItems
        {
            get
            {
                if (folder.Id == Guid.Empty)
                    return Array.Empty<MenuItem>();

                return new MenuItem[]
                {
                    new OsuMenuItem("Rename", MenuItemType.Standard, BeginEditing),
                    new OsuMenuItem("Delete folder", MenuItemType.Destructive, () => OnRequestDelete?.Invoke(folder)),
                };
            }
        }

        public void BeginEditing()
        {
            folderNameText.ClearTransforms();
            folderNameText.Alpha = 0;
            folderTextBox.Alpha = 1;
            folderTextBox.Text = folder.Name;
            GetContainingFocusManager()?.ChangeFocus(folderTextBox);
            folderTextBox.SelectAll();
        }

        private partial class FolderNameTextBox : OsuTextBox
        {
            protected override float LeftRightPadding => 4;

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundUnfocused = Color4.Transparent;
                BackgroundFocused = Color4.Black.Opacity(0.5f);
                BorderThickness = 0;
                CornerRadius = 4;
            }

            protected override void OnFocus(FocusEvent e)
            {
                base.OnFocus(e);
                BorderThickness = 0;
            }

            protected override Drawable GetDrawableCharacter(char c) => new OsuSpriteText
            {
                Text = c.ToString(),
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold)
            };
        }
    }
}
