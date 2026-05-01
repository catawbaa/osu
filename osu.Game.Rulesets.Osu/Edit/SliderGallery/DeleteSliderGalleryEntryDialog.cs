// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Confirmation dialog for deleting a slider gallery entry.
    /// </summary>
    public partial class DeleteSliderGalleryEntryDialog : DeletionDialog
    {
        public DeleteSliderGalleryEntryDialog(string entryName, Action deleteAction)
        {
            BodyText = $"Are you sure you want to delete \"{entryName}\" from the slider gallery?";
            DangerousAction = deleteAction;
        }
    }
}
