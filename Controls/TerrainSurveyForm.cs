using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    /// <summary>
    /// A photo survey of the drawn polygon for a 3D model of hills and mountain ranges: camera, height above the
    /// ground, overlaps, the lines' direction, an optional cross-hatched oblique pass, how the height follows the
    /// ground, and the split into flights by battery. Heights and distances in metres.
    /// </summary>
    public class TerrainSurveyForm : Form
    {
        /// <summary>Above this height over the ground a permit is needed in Vietnam.</summary>
        const double HighMetres = 120;

        readonly IList<PointLatLngAlt> polygon;
        readonly PointLatLngAlt home;
        readonly Func<double, double, double?> ground;

        readonly ComboBox camera = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        readonly NumericUpDown height = Number(5, 500, 0);
        readonly NumericUpDown frontOverlap = Number(50, 95, 0);
        readonly NumericUpDown sideOverlap = Number(30, 95, 0);
        readonly NumericUpDown angle = Number(0, 359, 0);
        readonly CheckBox oblique = new CheckBox { Text = FsemiText.Get("Terrain_Oblique"), AutoSize = true };
        readonly NumericUpDown obliquePitch = Number(20, 80, 0);
        readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        readonly CheckBox split = new CheckBox { Text = FsemiText.Get("Insp_Split"), AutoSize = true };
        readonly NumericUpDown battery = Number(3, 120, 0);
        readonly Label coverage = new Label { AutoSize = true, MaximumSize = new Size(440, 0), Margin = new Padding(3, 6, 3, 3) };
        readonly Label summary = new Label { AutoSize = true, MaximumSize = new Size(440, 0), Margin = new Padding(3, 10, 3, 3) };
        readonly Label flightsText = new Label { AutoSize = true, MaximumSize = new Size(440, 0), Margin = new Padding(3, 6, 3, 3) };
        readonly Label warning = new Label { AutoSize = true, MaximumSize = new Size(440, 0) };
        readonly MyButton ok = new MyButton { Text = FsemiText.Get("Common_AddToMission"), DialogResult = DialogResult.OK, Width = 110 };

        // the ground's lowest and highest point in the polygon, once looked up
        (double low, double high)? groundRange;

        public CameraSpec Camera => camera.SelectedItem as CameraSpec;
        public bool Split => split.Checked;

        /// <param name="polygon">The survey area.</param>
        /// <param name="home">The planned home, to time the flights from; null when unknown.</param>
        /// <param name="ground">The ground's height above sea level at a position, null where unknown.</param>
        /// <param name="defaultAngle">The lines' direction to start with, degrees from north.</param>
        public TerrainSurveyForm(IList<PointLatLngAlt> polygon, PointLatLngAlt home, Func<double, double, double?> ground,
            double defaultAngle)
        {
            this.polygon = polygon;
            this.home = home;
            this.ground = ground;

            Text = FsemiText.Get("Terrain_Title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            height.Value = Load("terrain_height", 60, height);
            frontOverlap.Value = Load("terrain_front_overlap", 80, frontOverlap);
            sideOverlap.Value = Load("terrain_side_overlap", 70, sideOverlap);
            angle.Value = (decimal)(Math.Round(defaultAngle) % 360);
            oblique.Checked = Settings.Instance.GetBoolean("terrain_oblique", true);
            obliquePitch.Value = Load("terrain_oblique_pitch", 45, obliquePitch);
            mode.Items.AddRange(new object[] { FsemiText.Get("Terrain_ModeTerrain"), FsemiText.Get("Terrain_ModeElevation") });
            mode.SelectedIndex = Settings.Instance.GetBoolean("terrain_from_elevation") ? 1 : 0;
            split.Checked = Settings.Instance.GetBoolean("inspection_split", true);
            battery.Value = (decimal)Math.Max(3, Math.Min(120, Math.Round(InspectionForm.EnduranceMinutes)));

            var saved = Settings.Instance.GetString("terrain_camera",
                Settings.Instance.GetString("inspection_camera", InspectionForm.DefaultCamera));
            foreach (var spec in CameraCoverage.Cameras())
            {
                camera.Items.Add(spec);
                if (spec.Name == saved)
                    camera.SelectedItem = spec;
            }

            if (camera.SelectedIndex < 0 && camera.Items.Count > 0)
                camera.SelectedIndex = 0;

            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
            AddWide(table, new Label
            {
                Text = FsemiText.Format("Terrain_Area", TerrainSurvey.Area(polygon) / 10000, polygon.Count), AutoSize = true,
                MaximumSize = new Size(440, 0), Margin = new Padding(3, 3, 3, 8)
            });
            AddRow(table, FsemiText.Get("Insp_Camera"), camera);
            AddRow(table, FsemiText.Get("Terrain_Height"), height);
            AddRow(table, FsemiText.Get("Insp_FrontOverlap"), frontOverlap);
            AddRow(table, FsemiText.Get("Terrain_SideOverlap"), sideOverlap);
            AddWide(table, coverage);
            AddRow(table, FsemiText.Get("Terrain_Angle"), angle);
            AddWide(table, oblique);
            AddRow(table, FsemiText.Get("Terrain_ObliquePitch"), obliquePitch);
            AddRow(table, FsemiText.Get("Terrain_Mode"), mode);
            AddWide(table, split);
            AddRow(table, FsemiText.Get("Insp_Battery"), battery);
            AddWide(table, summary);
            AddWide(table, flightsText);
            AddWide(table, warning);

            var cancel = new MyButton { Text = FsemiText.Get("Common_Cancel"), DialogResult = DialogResult.Cancel, Width = 80 };
            var buttons = new FlowLayoutPanel
                { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AddWide(table, buttons);
            FsemiLayout.FitItems(camera);
            FsemiLayout.FitItems(mode);
            FsemiLayout.FitText(ok);
            FsemiLayout.FitText(cancel);

            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;

            foreach (var n in new[] { height, frontOverlap, sideOverlap, angle, obliquePitch })
                n.ValueChanged += (s, e) => UpdateSummary();
            camera.SelectedIndexChanged += (s, e) => UpdateSummary();
            mode.SelectedIndexChanged += (s, e) => UpdateSummary();
            oblique.CheckedChanged += (s, e) => UpdateSummary();
            split.CheckedChanged += (s, e) => UpdateSummary();
            battery.ValueChanged += (s, e) =>
            {
                InspectionForm.EnduranceMinutes = (double)battery.Value;
                UpdateSummary();
            };

            try
            {
                groundRange = TerrainSurvey.GroundRange(polygon, 30, ground);
            }
            catch (Exception)
            {
                groundRange = null;
            }

            UpdateSummary();
            ThemeManager.ApplyThemeTo(this);
            warning.ForeColor = Color.FromArgb(0xe5, 0x48, 0x4d);
        }

        static NumericUpDown Number(int min, int max, int decimals)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = 80 };
        }

        /// <summary>A whole number setting, so it reads back in any culture.</summary>
        static decimal Load(string key, int fallback, NumericUpDown box)
        {
            return Math.Max(box.Minimum, Math.Min(box.Maximum, Settings.Instance.GetInt32(key, fallback)));
        }

        static void AddRow(TableLayoutPanel table, string caption, Control input)
        {
            table.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 3) });
            table.Controls.Add(input);
        }

        static void AddWide(TableLayoutPanel table, Control control)
        {
            table.Controls.Add(control);
            table.SetColumnSpan(control, 2);
        }

        public TerrainSurveyOptions Options => new TerrainSurveyOptions
        {
            Height = (double)height.Value,
            Mode = mode.SelectedIndex == 1 ? TerrainHeightMode.FromElevation : TerrainHeightMode.TerrainFrame,
            LineSpacing = Spacing(Camera, (double)height.Value, -90).line,
            PhotoSpacing = Spacing(Camera, (double)height.Value, -90).photo,
            ReturnToLaunch = true
        };

        /// <summary>
        /// Metres between lines and between photos along a line for the overlaps, the camera's long side across the
        /// line, from the slant distance to the ground at the pitch.
        /// </summary>
        (double line, double photo) Spacing(CameraSpec spec, double h, double pitch)
        {
            if (spec == null)
                return (Math.Max(1, h / 2), Math.Max(1, h / 4));
            var d = h / Math.Sin(Math.Abs(pitch) * Math.PI / 180);
            return (Math.Max(1, CameraCoverage.PhotoSpacing(spec, d, (double)sideOverlap.Value / 100)),
                Math.Max(1, CameraCoverage.LevelSpacing(spec, d, (double)frontOverlap.Value / 100)));
        }

        /// <summary>
        /// The survey's lines: straight down along the angle, then, with the oblique pass, across at right angles with
        /// the camera tilted.
        /// </summary>
        public List<SurveyLine> Lines()
        {
            var h = (double)height.Value;
            var down = Spacing(Camera, h, -90);
            var lines = TerrainSurvey.Lines(polygon, (double)angle.Value, down.line, -90, down.photo);
            if (oblique.Checked)
            {
                var pitch = -(double)obliquePitch.Value;
                var tilted = Spacing(Camera, h, pitch);
                lines.AddRange(TerrainSurvey.Lines(polygon, ((double)angle.Value + 90) % 360, tilted.line, pitch,
                    tilted.photo));
            }

            return lines;
        }

        public List<InspectionItem> Build(IList<SurveyLine> lines)
        {
            return TerrainSurvey.Build(lines, Options, ground);
        }

        public List<InspectionFlight> Flights()
        {
            var (speed, accel) = InspectionForm.WaypointSpeed();
            return TerrainSurvey.Flights(Lines(), Build, home, speed, accel, InspectionForm.FlightBudgetSeconds);
        }

        void UpdateSummary()
        {
            var spec = Camera;
            var h = (double)height.Value;
            var (lineSpacing, photoSpacing) = Spacing(spec, h, -90);
            coverage.Text = spec == null
                ? ""
                : FsemiText.Format("Terrain_Coverage", CameraCoverage.Gsd(spec, h), lineSpacing, photoSpacing);
            obliquePitch.Enabled = oblique.Checked;
            battery.Enabled = split.Checked;

            List<SurveyLine> lines;
            List<InspectionItem> items;
            try
            {
                lines = Lines();
                items = Build(lines);
            }
            catch (Exception ex)
            {
                summary.Text = "";
                flightsText.Text = "";
                warning.Text = ex.Message;
                ok.Enabled = false;
                return;
            }

            ok.Enabled = true;
            var (speed, accel) = InspectionForm.WaypointSpeed();
            var length = InspectionPattern.PathLength(items);
            var minutes = InspectionPattern.FlightTime(items, speed, accel) / 60;
            // photos along the lines; the turns between them take none
            var photos = lines.Sum(l => (int)(l.Start.GetDistance(l.End) / l.PhotoSpacing) + 1);
            var text = FsemiText.Format("Terrain_Summary", lines.Count, items.Count, photos, length / 1000, minutes, speed);
            text += " " + (groundRange.HasValue
                ? FsemiText.Format("Terrain_Ground", groundRange.Value.low, groundRange.Value.high,
                    groundRange.Value.high - groundRange.Value.low)
                : FsemiText.Get("Terrain_NoGround"));
            summary.Text = text;

            flightsText.Text = "";
            if (Split)
            {
                var list = Flights();
                var minutesEach = InspectionForm.FlightBudgetSeconds / 60;
                flightsText.Text = list.Count <= 1
                    ? FsemiText.Format("Insp_OneFlight", list.Count == 1 ? list[0].Seconds / 60 : 0, minutesEach,
                        InspectionForm.EnduranceMinutes)
                    : FsemiText.Format("Insp_Flights", list.Count, minutesEach, InspectionForm.EnduranceMinutes) + "\n" +
                      string.Join("\n", list.Select((f, i) =>
                          FsemiText.Format("Terrain_FlightLine", i + 1, f.FirstLine, f.LastLine, f.Seconds / 60)));
            }

            var warnings = new List<string>();
            if (h > HighMetres)
                warnings.Add(FsemiText.Format("Terrain_WarnHigh", HighMetres));
            if (mode.SelectedIndex == 0)
            {
                var terrain = DeliveryConfig.Param("TERRAIN_ENABLE");
                if (terrain == 0)
                    warnings.Add(FsemiText.Get("Terrain_WarnTerrainEnable"));
            }

            if (items.Count(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT) > InspectionPattern.MaxWaypoints / 2 && !Split)
                warnings.Add(FsemiText.Format("Insp_WarnItems", InspectionPattern.MaxWaypoints / 2));
            warning.Text = string.Join("\n", warnings);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (DialogResult != DialogResult.OK)
                return;

            var en = CultureInfo.InvariantCulture;
            Settings.Instance["terrain_height"] = ((int)height.Value).ToString(en);
            Settings.Instance["terrain_front_overlap"] = ((int)frontOverlap.Value).ToString(en);
            Settings.Instance["terrain_side_overlap"] = ((int)sideOverlap.Value).ToString(en);
            Settings.Instance["terrain_oblique"] = oblique.Checked.ToString();
            Settings.Instance["terrain_oblique_pitch"] = ((int)obliquePitch.Value).ToString(en);
            Settings.Instance["terrain_from_elevation"] = (mode.SelectedIndex == 1).ToString();
            Settings.Instance["inspection_split"] = Split.ToString();
            Settings.Instance["terrain_camera"] = Camera?.Name ?? "";
        }
    }
}
