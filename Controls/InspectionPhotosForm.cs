using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    /// <summary>
    /// Inspection photos after a flight: the photo folder from the camera's card and the flight's recorded photo
    /// positions in, named and geotagged copies with photos.csv, geo.txt and photos.kml out.
    /// </summary>
    public class InspectionPhotosForm : Form
    {
        readonly TextBox folder = new TextBox { Width = 320 };
        readonly ComboBox flights = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        readonly TextBox prefix = new TextBox { Width = 200 };
        readonly NumericUpDown floorHeight = new NumericUpDown { Minimum = 1, Maximum = 20, DecimalPlaces = 1, Increment = 0.1M, Width = 80 };
        readonly NumericUpDown groundHeight = new NumericUpDown { Minimum = -100, Maximum = 500, DecimalPlaces = 1, Increment = 0.5M, Width = 80 };
        readonly CheckBox geotag = new CheckBox { Text = FsemiText.Get("Photo_Geotag"), AutoSize = true, Checked = true };
        readonly Label output = new Label { AutoSize = true, MaximumSize = new Size(440, 0), ForeColor = Color.Gray };

        /// <summary>A positions file and how it shows in the list.</summary>
        class Flight
        {
            public string File;
            public string Text;
            public override string ToString() => Text;
        }

        public InspectionPhotosForm()
        {
            Text = FsemiText.Get("Photo_Title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            prefix.Text = Settings.Instance.GetString("inspection_photo_prefix", "Building");
            // whole centimetres, so they read back in any culture
            floorHeight.Value = Settings.Instance.GetInt32("inspection_floor_cm", 330) / 100M;
            groundHeight.Value = Settings.Instance.GetInt32("inspection_ground_cm", 0) / 100M;
            geotag.Checked = Settings.Instance.GetBoolean("inspection_geotag", true);
            LoadFlights();

            var browseFolder = new MyButton { Text = FsemiText.Get("Photo_Browse"), Width = 90 };
            browseFolder.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog { Description = FsemiText.Get("Photo_FolderPrompt") })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        folder.Text = dialog.SelectedPath;
            };
            var browseFlight = new MyButton { Text = FsemiText.Get("Photo_Browse"), Width = 90 };
            browseFlight.Click += (s, e) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "CSV|*" + PhotoRecorder.PhotosSuffix + ";*.csv", InitialDirectory = PhotoRecorder.Folder })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        var item = Describe(dialog.FileName);
                        flights.Items.Insert(0, item);
                        flights.SelectedIndex = 0;
                    }
            };

            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
            AddRow(table, "Photo_Folder", folder, browseFolder);
            AddRow(table, "Photo_Flight", flights, browseFlight);
            AddRow(table, "Photo_Prefix", prefix, null);
            AddRow(table, "Photo_FloorHeight", floorHeight, null);
            AddRow(table, "Photo_GroundHeight", groundHeight, null);
            table.Controls.Add(geotag);
            table.SetColumnSpan(geotag, 3);
            table.Controls.Add(output);
            table.SetColumnSpan(output, 3);

            var run = new MyButton { Text = FsemiText.Get("Photo_Run"), Width = 120, Height = 30 };
            run.Click += (s, e) => Run();
            var close = new MyButton { Text = FsemiText.Get("Common_Close"), Width = 120, Height = 30, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(close);
            buttons.Controls.Add(run);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            layout.Controls.Add(new Label { Text = FsemiText.Get("Photo_Intro"), AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(3, 3, 3, 10) });
            layout.Controls.Add(table);
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            CancelButton = close;

            folder.TextChanged += (s, e) => ShowOutput();
            prefix.TextChanged += (s, e) => ShowOutput();
            ShowOutput();
            FsemiLayout.FitText(run);
            FsemiLayout.FitText(close);
            ThemeManager.ApplyThemeTo(this);
        }

        static void AddRow(TableLayoutPanel table, string label, Control field, Control button)
        {
            table.Controls.Add(new Label { Text = FsemiText.Get(label), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 3) });
            table.Controls.Add(field);
            if (button != null)
                table.Controls.Add(button);
            else
                table.Controls.Add(new Label { AutoSize = true });
        }

        /// <summary>The recorded flights with photos, newest first.</summary>
        void LoadFlights()
        {
            flights.Items.Clear();
            if (Directory.Exists(PhotoRecorder.Folder))
                foreach (var file in Directory.GetFiles(PhotoRecorder.Folder, "*" + PhotoRecorder.PhotosSuffix)
                             .OrderByDescending(f => f))
                    flights.Items.Add(Describe(file));
            if (flights.Items.Count > 0)
                flights.SelectedIndex = 0;
        }

        static Flight Describe(string file)
        {
            int count;
            try
            {
                count = InspectionPhotos.ReadPositions(File.ReadAllText(file)).Count;
            }
            catch (Exception)
            {
                count = 0;
            }

            return new Flight
            {
                File = file,
                Text = FsemiText.Format("Photo_FlightItem", File.GetLastWriteTime(file), count, Path.GetFileName(file))
            };
        }

        string OutputFolder => folder.Text.Trim() == "" ? "" :
            Path.Combine(folder.Text.Trim(), "FSEMI_" + InspectionPhotos.Safe(prefix.Text));

        void ShowOutput()
        {
            output.Text = OutputFolder == "" ? "" : FsemiText.Format("Photo_Output", OutputFolder);
        }

        void Run()
        {
            if (!Directory.Exists(folder.Text.Trim()))
            {
                CustomMessageBox.Show(FsemiText.Get("Photo_NoFolder"), Text);
                return;
            }

            if (!(flights.SelectedItem is Flight flight))
            {
                CustomMessageBox.Show(FsemiText.Get("Photo_NoFlight"), Text);
                return;
            }

            Settings.Instance["inspection_photo_prefix"] = prefix.Text.Trim();
            Settings.Instance["inspection_floor_cm"] = ((int)(floorHeight.Value * 100)).ToString(CultureInfo.InvariantCulture);
            Settings.Instance["inspection_ground_cm"] = ((int)(groundHeight.Value * 100)).ToString(CultureInfo.InvariantCulture);
            Settings.Instance["inspection_geotag"] = geotag.Checked.ToString();

            var options = new InspectionPhotoOptions
            {
                Prefix = prefix.Text, FloorHeight = (double)floorHeight.Value, GroundHeight = (double)groundHeight.Value
            };
            var source = folder.Text.Trim();
            var target = OutputFolder;
            var write = geotag.Checked;
            InspectionPhotoFiles.Result result = null;
            string error = null;

            IProgressReporterDialogue progress = new ProgressReporterDialogue
                { StartPosition = FormStartPosition.CenterScreen, Text = Text };
            progress.DoWork += sender =>
            {
                try
                {
                    var positions = InspectionPhotos.ReadPositions(File.ReadAllText(flight.File));
                    result = InspectionPhotoFiles.Process(source, positions, options, target, write,
                        (percent, file) => sender.UpdateProgressAndStatus(percent, file),
                        () => sender.doWorkArgs.CancelRequested);
                }
                catch (OperationCanceledException)
                {
                    sender.doWorkArgs.CancelAcknowledged = true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            };
            progress.UpdateProgressAndStatus(-1, Text);
            ThemeManager.ApplyThemeTo(progress);
            progress.RunBackgroundOperationAsync();
            progress.Dispose();

            if (error != null)
            {
                CustomMessageBox.Show(error, Strings.ERROR);
                return;
            }

            if (result == null)
                return;

            var text = FsemiText.Format(result.ByOrder ? "Photo_DoneOrder" : "Photo_DoneTime", result.Placed,
                           result.Photos.Count) +
                       (result.Unplaced > 0 ? "\n" + FsemiText.Format("Photo_Unplaced", result.Unplaced) : "") +
                       "\n\n" + InspectionPhotos.Summary(result.Photos) + "\n\n" + FsemiText.Format("Photo_Files", target);
            if (CustomMessageBox.Show(text, Text, CustomMessageBox.MessageBoxButtons.YesNo,
                    CustomMessageBox.MessageBoxIcon.Information, FsemiText.Get("Photo_OpenFolder"),
                    FsemiText.Get("Common_Close")) == CustomMessageBox.DialogResult.Yes)
            {
                try
                {
                    Process.Start(target);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.Message, Strings.ERROR);
                }
            }
        }
    }
}
