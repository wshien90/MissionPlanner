using System;
using System.Windows.Forms;

namespace MissionPlanner.Controls
{
    /// <summary>Widens controls to fit their text, which is longer in some languages than in English.</summary>
    static class FsemiLayout
    {
        /// <summary>At least as wide as its longest item and the drop-down button.</summary>
        public static void FitItems(ComboBox box)
        {
            var widest = 0;
            foreach (var item in box.Items)
                widest = Math.Max(widest, TextRenderer.MeasureText(box.GetItemText(item), box.Font).Width);
            box.Width = Math.Max(box.Width, widest + SystemInformation.VerticalScrollBarWidth + 12);
            box.DropDownWidth = Math.Max(box.DropDownWidth, box.Width);
        }

        /// <summary>At least as wide as its text and some padding.</summary>
        public static void FitText(Control control, int padding = 24)
        {
            control.Width = Math.Max(control.Width, TextRenderer.MeasureText(control.Text, control.Font).Width + padding);
        }
    }
}
