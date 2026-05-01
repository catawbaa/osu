// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// The main panel that displays all saved sliders in the gallery,
    /// organized by folders with drag/drop support.
    /// </summary>
    public partial class SliderGalleryPanel : CompositeDrawable
    {
        private const float compact_spacing = 4;
        private const int compact_columns = 3;
        private const float content_padding = 6;

        private FillFlowContainer cardContainer = null!;
        private OsuScrollContainer scrollContainer = null!;
        private Container dragProxyContainer = null!;

        [Resolved]
        private SliderGalleryStorage galleryStorage { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        /// <summary>
        /// Tracks which folders are expanded (by folder ID).
        /// </summary>
        private readonly HashSet<Guid> expandedFolders = new HashSet<Guid>();

        private Guid? folderToEditNext;


        /// <summary>
        /// The entry currently being dragged, if any.
        /// </summary>
        internal SliderGalleryEntry? DraggedEntry { get; set; }

        private OverlayColourProvider colourProvider = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;

            RelativeSizeAxes = Axes.X;
            Height = 300;

            InternalChildren = new Drawable[]
            {
                new OsuContextMenuContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new BackgroundContextMenuArea
                        {
                            RelativeSizeAxes = Axes.Both,
                            OnRequestAddFolder = addFolder
                        },
                        scrollContainer = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = cardContainer = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = Vector2.Zero,
                                Padding = new MarginPadding { Horizontal = 6, Vertical = 6 },
                            },
                        }
                    }
                },
                dragProxyContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

            refreshEntries();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            galleryStorage.EntriesChanged += () => Scheduler.AddOnce(refreshEntries);
        }

        private void refreshEntries()
        {
            cardContainer.Clear();

            var folders = galleryStorage.GetFolders();
            var rootEntries = galleryStorage.GetAll();

            // Render folders.
            var allFolders = new List<SliderGalleryFolder>(folders);
            if (rootEntries.Count > 0)
            {
                allFolders.Add(new SliderGalleryFolder { Id = Guid.Empty, Name = "Uncategorized" });
            }

            foreach (var folder in allFolders)
            {
                bool isUncategorized = folder.Id == Guid.Empty;
                bool isExpanded = expandedFolders.Contains(folder.Id);
                var entriesInFolder = isUncategorized ? rootEntries : galleryStorage.GetEntriesInFolder(folder.Id);

                // In compact mode, folder headers still span the full width.
                var header = new SliderGalleryFolderHeader(folder, isExpanded, entriesInFolder.Count)
                {
                    OnToggleExpanded = toggleFolder,
                    OnRequestDelete = requestDeleteFolder,
                    OnRequestRename = requestRenameFolder,
                };

                if (folder.Id == folderToEditNext)
                {
                    Schedule(() => header.BeginEditing());
                    folderToEditNext = null;
                }



                var folderFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[] { header }
                };

                if (isExpanded)
                {
                    var contentFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = Vector2.Zero,
                        Padding = new MarginPadding(6),
                    };

                    foreach (var entry in entriesInFolder)
                    {
                        contentFlow.Add(new SliderGalleryEntryCard(entry)
                        {
                            OnPlace = placeSlider,
                            OnRequestDelete = requestDeleteEntry,
                            OnRequestRename = requestRenameEntry,
                            OnRequestMoveToFolder = moveEntryToFolder,
                        });
                    }

                    folderFlow.Add(new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new BlockContextMenuBox
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background4,
                            },
                            contentFlow
                        }
                    });
                }

                // Wrap the folder flow in a full-width container so it breaks the flow.
                cardContainer.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Margin = new MarginPadding { Top = 4 },
                    Masking = true,
                    CornerRadius = 4,
                    Child = folderFlow,
                });
            }

            if (folders.Count == 0 && rootEntries.Count == 0)
            {
                cardContainer.Add(new OsuTextFlowContainer(t =>
                {
                    t.Font = OsuFont.GetFont(size: 12, italics: true);
                    t.Colour = Colour4.Gray;
                })
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    TextAnchor = Anchor.TopCentre,
                    Padding = new MarginPadding(8),
                    Text = "Gallery is empty.",
                });
            }
        }

        private void addFolder()
        {
            var folder = galleryStorage.AddFolder("New folder");
            expandedFolders.Add(folder.Id);
            folderToEditNext = folder.Id;
        }

        private void toggleFolder(SliderGalleryFolder folder)
        {
            if (!expandedFolders.Remove(folder.Id))
                expandedFolders.Add(folder.Id);

            refreshEntries();
        }

        private void placeSlider(SliderGalleryEntry entry)
        {
            var slider = galleryStorage.CreateSliderFromEntry(entry, editorClock.CurrentTime);

            editorBeatmap.BeginChange();
            editorBeatmap.Add(slider);
            editorBeatmap.SelectedHitObjects.Clear();
            editorBeatmap.SelectedHitObjects.Add(slider);
            editorBeatmap.EndChange();
        }

        private void requestDeleteEntry(SliderGalleryEntry entry)
        {
            galleryStorage.Remove(entry.Id);
        }

        private void requestRenameEntry(SliderGalleryEntry entry, string newName)
        {
            galleryStorage.Rename(entry.Id, newName);
        }

        private void moveEntryToFolder(SliderGalleryEntry entry, Guid? folderId)
        {
            galleryStorage.MoveToFolder(entry.Id, folderId);
        }

        private void requestDeleteFolder(SliderGalleryFolder folder)
        {
            galleryStorage.RemoveFolder(folder.Id);
        }

        private void requestRenameFolder(SliderGalleryFolder folder, string newName)
        {
            galleryStorage.RenameFolder(folder.Id, newName);
        }

        /// <summary>
        /// Handles a drag drop: finds the folder header under the cursor and moves the entry there.
        /// </summary>
        internal void HandleDrop(SliderGalleryEntry entry, DragEndEvent e)
        {
            var screenPos = e.ScreenSpaceMousePosition;

            // Check if we dropped onto a folder header.
            foreach (var child in cardContainer.Children)
            {
                if (child is Container c && c.Children.Count == 1 && c.Children[0] is FillFlowContainer flow)
                {
                    foreach (var fChild in flow.Children)
                    {
                        if (fChild is SliderGalleryFolderHeader header && header.ReceivePositionalInputAt(screenPos))
                        {
                            galleryStorage.MoveToFolder(entry.Id, header.FolderId);
                            return;
                        }
                    }
                }
            }

            // Not dropped on a folder — move to root.
            galleryStorage.MoveToFolder(entry.Id, null);
        }

        /// <summary>
        /// Updates folder header drop target highlighting during a drag.
        /// </summary>
        internal void UpdateDragHighlight(Vector2 screenSpacePosition)
        {
            foreach (var child in cardContainer.Children)
            {
                if (child is Container c && c.Children.Count == 1 && c.Children[0] is FillFlowContainer flow)
                {
                    foreach (var fChild in flow.Children)
                    {
                        if (fChild is SliderGalleryFolderHeader header)
                            header.IsDropTarget = header.ReceivePositionalInputAt(screenSpacePosition);
                    }
                }
            }
        }

        /// <summary>
        /// Clears all drop target highlighting.
        /// </summary>
        internal void ClearDragHighlight()
        {
            foreach (var child in cardContainer.Children)
            {
                if (child is Container c && c.Children.Count == 1 && c.Children[0] is FillFlowContainer flow)
                {
                    foreach (var fChild in flow.Children)
                    {
                        if (fChild is SliderGalleryFolderHeader header)
                            header.IsDropTarget = false;
                    }
                }
            }
        }

        internal void AddDragProxy(Drawable proxy)
        {
            dragProxyContainer.Add(proxy);
        }

        internal void RemoveDragProxy(Drawable proxy)
        {
            dragProxyContainer.Remove(proxy, true);
        }

        internal void UpdateDragProxyPosition(Drawable proxy, Vector2 screenSpaceMousePosition)
        {
            proxy.Position = dragProxyContainer.ToLocalSpace(screenSpaceMousePosition);
        }

        private partial class BackgroundContextMenuArea : CompositeDrawable, IHasContextMenu
        {
            public Action? OnRequestAddFolder;

            public override bool HandlePositionalInput => true;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                };
            }

            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem("Add Folder", MenuItemType.Standard, () => OnRequestAddFolder?.Invoke())
            };
        }

        private partial class BlockContextMenuBox : Box, IHasContextMenu
        {
            public override bool HandlePositionalInput => true;
            public MenuItem[] ContextMenuItems => Array.Empty<MenuItem>();
        }
    }
}
