using System;
using System.Drawing;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    /// <summary>
    /// Settings for a delivery drop added to the mission: altitudes, hover times, how to release and what to do
    /// after. Altitudes are in the planner's display units.
    /// </summary>
    public class DeliveryDropForm : Form
    {
        readonly NumericUpDown cruiseAlt = Number(1, 10000, 0);
        readonly NumericUpDown dropAlt = Number(1, 10000, 0);
        readonly NumericUpDown hoverBefore = Number(0, 120, 0);
        readonly NumericUpDown hoverAfter = Number(0, 120, 0);
        readonly ComboBox method = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        readonly CheckBox lockAfter = new CheckBox { Text = FsemiText.Get("Drop_LockAfter"), AutoSize = true, Checked = true };
        readonly CheckBox returnHome = new CheckBox { Text = FsemiText.Get("Drop_Rtl"), AutoSize = true, Checked = true };
        readonly Label summary = new Label { AutoSize = true, MaximumSize = new Size(360, 0), Margin = new Padding(3, 10, 3, 3) };

        public double CruiseAlt => (double)cruiseAlt.Value;
        public double DropAlt => (double)dropAlt.Value;
        public double HoverBefore => (double)hoverBefore.Value;
        public double HoverAfter => (double)hoverAfter.Value;
        public DeliveryMethod Method => method.SelectedIndex == 0 ? DeliveryMethod.Gripper : DeliveryMethod.Servo;
        public bool LockAfter => lockAfter.Checked;
        public bool ReturnHome => returnHome.Checked;

        public DeliveryDropForm(double defaultCruiseAlt)
        {
            Text = FsemiText.Get("Drop_Title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            var unit = " (" + CurrentState.AltUnit + ")";
            cruiseAlt.Value = Clamp(defaultCruiseAlt, cruiseAlt);
            dropAlt.Value = Clamp(Settings.Instance.GetDouble("delivery_drop_alt", 10 * CurrentState.multiplieralt), dropAlt);
            hoverBefore.Value = Clamp(Settings.Instance.GetDouble("delivery_hover_before", 3), hoverBefore);
            hoverAfter.Value = Clamp(Settings.Instance.GetDouble("delivery_hover_after", 3), hoverAfter);
            lockAfter.Checked = Settings.Instance.GetBoolean("delivery_lock_after", true);
            returnHome.Checked = Settings.Instance.GetBoolean("delivery_return_home", true);

            method.Items.Add(FsemiText.Get("Drop_Gripper"));
            method.Items.Add(FsemiText.Format("Drop_Servo", DeliveryConfig.Servo,
                DeliveryConfig.ReleasePwm, DeliveryConfig.LockPwm));
            method.SelectedIndex = DeliveryConfig.Resolve(DeliveryConfig.Method) == DeliveryMethod.Gripper ? 0 : 1;

            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
            AddRow(table, FsemiText.Get("Drop_Cruise") + unit, cruiseAlt);
            AddRow(table, FsemiText.Get("Drop_DropAlt") + unit, dropAlt);
            AddRow(table, FsemiText.Get("Drop_HoverBefore"), hoverBefore);
            AddRow(table, FsemiText.Get("Drop_HoverAfter"), hoverAfter);
            AddRow(table, FsemiText.Get("Drop_With"), method);
            table.Controls.Add(lockAfter);
            table.SetColumnSpan(lockAfter, 2);
            table.Controls.Add(returnHome);
            table.SetColumnSpan(returnHome, 2);
            table.Controls.Add(summary);
            table.SetColumnSpan(summary, 2);

            var ok = new MyButton { Text = FsemiText.Get("Common_AddToMission"), DialogResult = DialogResult.OK, Width = 110 };
            var cancel = new MyButton { Text = FsemiText.Get("Common_Cancel"), DialogResult = DialogResult.Cancel, Width = 80 };
            var buttons = new FlowLayoutPanel
                { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            table.Controls.Add(buttons);
            table.SetColumnSpan(buttons, 2);
            FsemiLayout.FitItems(method);
            FsemiLayout.FitText(ok);
            FsemiLayout.FitText(cancel);

            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;

            foreach (Control c in new Control[] { cruiseAlt, dropAlt, hoverBefore, hoverAfter, method, lockAfter, returnHome })
            {
                if (c is NumericUpDown n)
                    n.ValueChanged += (s, e) => UpdateSummary();
                else if (c is ComboBox b)
                    b.SelectedIndexChanged += (s, e) => UpdateSummary();
                else if (c is CheckBox k)
                    k.CheckedChanged += (s, e) => UpdateSummary();
            }

            UpdateSummary();
            ThemeManager.ApplyThemeTo(this);
        }

        static NumericUpDown Number(int min, int max, int decimals)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = 80 };
        }

        static decimal Clamp(double value, NumericUpDown box)
        {
            return Math.Max(box.Minimum, Math.Min(box.Maximum, (decimal)Math.Round(value, box.DecimalPlaces)));
        }

        static void AddRow(TableLayoutPanel table, string caption, Control input)
        {
            table.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 3) });
            table.Controls.Add(input);
        }

        void UpdateSummary()
        {
            var unit = CurrentState.AltUnit;
            var text = FsemiText.Format("Drop_Summary", CruiseAlt, DropAlt, unit, HoverBefore, HoverAfter);
            if (LockAfter)
                text += FsemiText.Get("Drop_SummaryLock");
            if (ReturnHome)
                text += FsemiText.Get("Drop_SummaryRtl");
            else
                text += FsemiText.Get("Drop_SummaryHold");
            summary.Text = text + ".";
            summary.ForeColor = DropAlt >= CruiseAlt ? Color.FromArgb(0xe5, 0x48, 0x4d) : ThemeManager.TextColor;
            if (DropAlt >= CruiseAlt)
                summary.Text += "\n" + FsemiText.Get("Drop_WarnAltitude");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (DialogResult != DialogResult.OK)
                return;

            Settings.Instance["delivery_drop_alt"] = DropAlt.ToString();
            Settings.Instance["delivery_hover_before"] = HoverBefore.ToString();
            Settings.Instance["delivery_hover_after"] = HoverAfter.ToString();
            Settings.Instance["delivery_lock_after"] = LockAfter.ToString();
            Settings.Instance["delivery_return_home"] = ReturnHome.ToString();
        }
    }
}
