using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    public enum InspectionKind
    {
        Orbit,
        Facade
    }

    /// <summary>
    /// Settings for an inspection pattern added to the mission: an orbit around a structure or passes along a wall.
    /// Distances and altitudes are in the planner's display units; the summary shows what the pattern adds.
    /// </summary>
    public class InspectionForm : Form
    {
        // closer than this to the structure the GPS error and wind matter
        const double CloseMetres = 5;

        // bigW's camera, until another is chosen
        public const string DefaultCamera = "Sony A6400 16mm (A6100, A6600)";

        // a mission this long may not fit the vehicle's storage
        const int ManyItems = 700;

        readonly InspectionKind kind;
        readonly Func<InspectionForm, List<InspectionItem>> build;
        readonly Func<InspectionForm, List<InspectionFlight>> flights;

        readonly NumericUpDown distance = Number(1, 1000, 1);
        readonly NumericUpDown bottomAlt = Number(1, 10000, 1);
        readonly NumericUpDown topAlt = Number(1, 10000, 1);
        readonly NumericUpDown levelSpacing = Number(1, 1000, 1);
        readonly NumericUpDown photosPerOrbit = Number(3, 120, 0);
        readonly NumericUpDown photoSpacing = Number(1, 1000, 1);
        readonly NumericUpDown hover = Number(0, 60, 1);
        readonly ComboBox camera = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        readonly NumericUpDown frontOverlap = Number(50, 95, 0);
        readonly NumericUpDown sideOverlap = Number(30, 95, 0);
        readonly NumericUpDown structureRadius = Number(0, 1000, 1);
        readonly Label coverage = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 6, 3, 3) };
        // while the camera sets the spacing boxes
        bool applying;
        readonly ComboBox direction = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        readonly ComboBox side = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        readonly CheckBox photos = new CheckBox { Text = FsemiText.Get("Insp_Photos"), AutoSize = true };
        readonly CheckBox returnHome = new CheckBox { Text = FsemiText.Get("Insp_Rtl"), AutoSize = true };
        readonly CheckBox split = new CheckBox { Text = FsemiText.Get("Insp_Split"), AutoSize = true };
        readonly NumericUpDown battery = Number(3, 120, 0);
        readonly Label flightsText = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 6, 3, 3) };
        readonly Label summary = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 10, 3, 3) };
        readonly Label warning = new Label { AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = Color.FromArgb(0xe5, 0x48, 0x4d) };
        readonly MyButton ok = new MyButton { Text = FsemiText.Get("Common_AddToMission"), DialogResult = DialogResult.OK, Width = 110 };

        /// <summary>Radius of the orbit or distance from the wall, in display units.</summary>
        public double Distance => (double)distance.Value;
        public double BottomAlt => (double)bottomAlt.Value;
        public double TopAlt => (double)topAlt.Value;
        public double LevelSpacing => (double)levelSpacing.Value;
        public int PhotosPerOrbit => (int)photosPerOrbit.Value;
        public double PhotoSpacing => (double)photoSpacing.Value;
        public double Hover => (double)hover.Value;
        public bool Clockwise => direction.SelectedIndex == 0;
        /// <summary>For a facade: the first of the two sides offered was chosen.</summary>
        public bool FirstSide => side.SelectedIndex == 0;
        public bool Photos => photos.Checked;
        public bool ReturnHome => returnHome.Checked;
        /// <summary>Split into flights by battery: each one ends with RTL.</summary>
        public bool Split => split.Checked;
        /// <summary>The camera the spacing is worked out for, or null when set by hand.</summary>
        public CameraSpec Camera => camera.SelectedItem as CameraSpec;

        /// <param name="build">Makes the pattern from the form's values, for the summary.</param>
        /// <param name="approachText">Where the vehicle comes in from.</param>
        /// <param name="wallText">For a facade, a line describing the wall.</param>
        /// <param name="sideNames">For a facade, the two sides of the wall to choose from.</param>
        /// <param name="flights">Makes the pattern as flights that each fit a battery.</param>
        public InspectionForm(InspectionKind kind, double defaultAlt, Func<InspectionForm, List<InspectionItem>> build,
            Func<InspectionForm, List<InspectionFlight>> flights, string approachText, string wallText = null,
            string[] sideNames = null)
        {
            this.kind = kind;
            this.build = build;
            this.flights = flights;

            Text = FsemiText.Get(kind == InspectionKind.Orbit ? "Insp_TitleOrbit" : "Insp_TitleFacade");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            var alt = " (" + CurrentState.AltUnit + ")";
            var dist = " (" + CurrentState.DistanceUnit + ")";
            var prefix = "inspection_" + kind.ToString().ToLowerInvariant() + "_";
            distance.Value = LoadValue(prefix + "distance", (kind == InspectionKind.Orbit ? 15 : 10) * CurrentState.multiplierdist, distance);
            topAlt.Value = LoadValue(prefix + "top", defaultAlt, topAlt);
            bottomAlt.Value = LoadValue(prefix + "bottom", Math.Min(10 * CurrentState.multiplieralt, defaultAlt), bottomAlt);
            levelSpacing.Value = LoadValue(prefix + "level_spacing", 5 * CurrentState.multiplieralt, levelSpacing);
            photosPerOrbit.Value = LoadValue(prefix + "photos_per_orbit", 12, photosPerOrbit);
            photoSpacing.Value = LoadValue(prefix + "photo_spacing", 5 * CurrentState.multiplierdist, photoSpacing);
            hover.Value = LoadValue(prefix + "hover", 1, hover);
            frontOverlap.Value = LoadValue("inspection_front_overlap", 80, frontOverlap);
            sideOverlap.Value = LoadValue("inspection_side_overlap", 70, sideOverlap);
            structureRadius.Value = LoadValue(prefix + "structure_radius", 5 * CurrentState.multiplierdist, structureRadius);
            camera.Items.Add(FsemiText.Get("Insp_CameraManual"));
            var saved = Settings.Instance.GetString("inspection_camera", DefaultCamera);
            foreach (var spec in CameraCoverage.Cameras())
            {
                camera.Items.Add(spec);
                if (spec.Name == saved)
                    camera.SelectedItem = spec;
            }

            if (camera.SelectedIndex < 0)
                camera.SelectedIndex = 0;
            photos.Checked = Settings.Instance.GetBoolean(prefix + "photos", true);
            returnHome.Checked = Settings.Instance.GetBoolean(prefix + "rtl", true);
            split.Checked = Settings.Instance.GetBoolean("inspection_split", true);
            battery.Value = (decimal)Math.Max(3, Math.Min(120, Math.Round(EnduranceMinutes)));

            direction.Items.AddRange(new object[] { FsemiText.Get("Insp_Clockwise"), FsemiText.Get("Insp_CounterClockwise") });
            direction.SelectedIndex = Settings.Instance.GetBoolean(prefix + "clockwise", true) ? 0 : 1;
            side.Items.AddRange(sideNames ?? new[] { FsemiText.Get("Insp_Left"), FsemiText.Get("Insp_Right") });
            side.SelectedIndex = 0;

            var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
            foreach (var text in new[] { wallText, approachText })
            {
                if (text == null)
                    continue;
                var label = new Label { Text = text, AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 3, 3, 8) };
                table.Controls.Add(label);
                table.SetColumnSpan(label, 2);
            }

            AddRow(table, FsemiText.Get("Insp_Camera"), camera);
            AddRow(table, FsemiText.Get("Insp_FrontOverlap"), frontOverlap);
            AddRow(table, FsemiText.Get("Insp_SideOverlap"), sideOverlap);
            if (kind == InspectionKind.Orbit)
                AddRow(table, FsemiText.Get("Insp_StructureRadius") + dist, structureRadius);
            AddWide(table, coverage);

            if (kind == InspectionKind.Orbit)
            {
                AddRow(table, FsemiText.Get("Insp_Radius") + dist, distance);
                AddRow(table, FsemiText.Get("Insp_PhotosPerOrbit"), photosPerOrbit);
                AddRow(table, FsemiText.Get("Insp_Direction"), direction);
            }
            else
            {
                AddRow(table, FsemiText.Get("Insp_Distance") + dist, distance);
                AddRow(table, FsemiText.Get("Insp_Side"), side);
                AddRow(table, FsemiText.Get("Insp_PhotoSpacing") + dist, photoSpacing);
            }

            AddRow(table, FsemiText.Get("Insp_Top") + alt, topAlt);
            AddRow(table, FsemiText.Get("Insp_Bottom") + alt, bottomAlt);
            AddRow(table, FsemiText.Get("Insp_LevelSpacing") + alt, levelSpacing);
            AddRow(table, FsemiText.Get("Insp_Hover"), hover);
            AddWide(table, photos);
            AddWide(table, returnHome);
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
            FsemiLayout.FitItems(direction);
            FsemiLayout.FitItems(camera);
            FsemiLayout.FitItems(side);
            FsemiLayout.FitText(ok);
            FsemiLayout.FitText(cancel);

            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;

            foreach (var n in new[] { distance, bottomAlt, topAlt, levelSpacing, photosPerOrbit, photoSpacing, hover })
                n.ValueChanged += (s, e) => UpdateSummary();
            foreach (var n in new[] { distance, frontOverlap, sideOverlap, structureRadius })
                n.ValueChanged += (s, e) => ApplyCamera();
            camera.SelectedIndexChanged += (s, e) => ApplyCamera();
            direction.SelectedIndexChanged += (s, e) => UpdateSummary();
            side.SelectedIndexChanged += (s, e) => UpdateSummary();
            photos.CheckedChanged += (s, e) => UpdateSummary();
            returnHome.CheckedChanged += (s, e) => UpdateSummary();
            split.CheckedChanged += (s, e) => UpdateSummary();
            battery.ValueChanged += (s, e) =>
            {
                EnduranceMinutes = (double)battery.Value;
                UpdateSummary();
            };

            ApplyCamera();
            UpdateSummary();
            ThemeManager.ApplyThemeTo(this);
            warning.ForeColor = Color.FromArgb(0xe5, 0x48, 0x4d);
        }

        static NumericUpDown Number(int min, int max, int decimals)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = 80 };
        }

        static decimal LoadValue(string key, double fallback, NumericUpDown box)
        {
            var value = Settings.Instance.GetDouble(key, fallback);
            if (double.IsNaN(value) || double.IsInfinity(value))
                value = fallback;
            // clamp as double first: a huge setting would overflow decimal
            value = Math.Max((double)box.Minimum, Math.Min((double)box.Maximum, value));
            return (decimal)Math.Round(value, box.DecimalPlaces);
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

        /// <summary>Metres from the lens to the structure's surface.</summary>
        double SurfaceDistance => Math.Max(CameraCoverage.MinSurfaceDistance,
            (Distance - (kind == InspectionKind.Orbit ? (double)structureRadius.Value : 0)) / CurrentState.multiplierdist);

        /// <summary>With a camera chosen, the photo and level spacing that give the overlaps, and what a photo covers.</summary>
        void ApplyCamera()
        {
            if (applying)
                return;
            var spec = Camera;
            foreach (var box in new Control[] { photosPerOrbit, photoSpacing, levelSpacing })
                box.Enabled = spec == null;
            frontOverlap.Enabled = sideOverlap.Enabled = structureRadius.Enabled = spec != null;
            if (spec == null)
            {
                coverage.Text = FsemiText.Get("Insp_CameraManualNote");
                return;
            }

            var d = SurfaceDistance;
            var front = (double)frontOverlap.Value / 100;
            var side = (double)sideOverlap.Value / 100;
            applying = true;
            try
            {
                if (kind == InspectionKind.Orbit)
                    photosPerOrbit.Value = Clamp(CameraCoverage.OrbitPhotos(spec, Distance / CurrentState.multiplierdist,
                        (double)structureRadius.Value / CurrentState.multiplierdist, front), photosPerOrbit);
                else
                    photoSpacing.Value = Clamp(CameraCoverage.PhotoSpacing(spec, d, front) * CurrentState.multiplierdist, photoSpacing);
                levelSpacing.Value = Clamp(CameraCoverage.LevelSpacing(spec, d, side) * CurrentState.multiplieralt, levelSpacing);
            }
            finally
            {
                applying = false;
            }

            var (width, height) = CameraCoverage.Footprint(spec, d);
            coverage.Text = FsemiText.Format("Insp_Coverage", CameraCoverage.Gsd(spec, d), d, width, height,
                CameraCoverage.DistanceFor(spec, 2));
            UpdateSummary();
        }

        static decimal Clamp(double value, NumericUpDown box)
        {
            value = Math.Max((double)box.Minimum, Math.Min((double)box.Maximum, value));
            return (decimal)Math.Round(value, box.DecimalPlaces);
        }

        void UpdateSummary()
        {
            List<InspectionItem> items;
            try
            {
                items = build(this);
            }
            catch (ArgumentException ex)
            {
                summary.Text = "";
                warning.Text = ex.Message;
                ok.Enabled = false;
                return;
            }

            ok.Enabled = true;
            var waypoints = items.Count(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT);
            var photoCount = items.Count(i => i.Cmd == MAVLink.MAV_CMD.DO_DIGICAM_CONTROL);
            var levels = items.Where(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT).Select(i => i.Alt).Distinct().Count();
            var length = InspectionPattern.PathLength(items);

            var (speed, accel) = WaypointSpeed();
            var minutes = InspectionPattern.FlightTime(items, speed, accel) / 60;

            summary.Text = string.Join(" ",
                FsemiText.Format("Insp_Summary", items.Count, levels, Math.Max(TopAlt, BottomAlt),
                    Math.Min(TopAlt, BottomAlt), CurrentState.AltUnit, waypoints, photoCount,
                    length * CurrentState.multiplierdist, CurrentState.DistanceUnit, minutes, speed),
                FsemiText.Get(ReturnHome ? "Insp_SummaryRtl" : "Insp_SummaryHold"),
                FsemiText.Get(kind == InspectionKind.Orbit ? "Insp_CameraOrbit" : "Insp_CameraFacade"),
                FsemiText.Get(ReturnHome ? "Insp_ClearRtl" : "Insp_Clear"));

            flightsText.Text = "";
            returnHome.Enabled = !Split;
            battery.Enabled = Split;
            if (Split && flights != null)
            {
                var list = flights(this);
                var minutesEach = FlightBudgetSeconds / 60;
                flightsText.Text = list.Count <= 1
                    ? FsemiText.Format("Insp_OneFlight", list.Count == 1 ? list[0].Seconds / 60 : 0, minutesEach,
                        EnduranceMinutes)
                    : FsemiText.Format("Insp_Flights", list.Count, minutesEach, EnduranceMinutes) + "\n" +
                      string.Join("\n", list.Select((f, i) => FsemiText.Format("Insp_FlightLine", i + 1,
                          f.TopAlt * CurrentState.multiplieralt, f.BottomAlt * CurrentState.multiplieralt,
                          CurrentState.AltUnit, f.Seconds / 60)));
            }

            var warnings = new List<string>();
            // an orbit flies straight between its points, so it passes closer than the radius
            var closest = kind == InspectionKind.Orbit
                ? InspectionPattern.ClosestApproach(Distance / CurrentState.multiplierdist, PhotosPerOrbit)
                : Distance / CurrentState.multiplierdist;
            if (closest < CloseMetres)
                warnings.Add(FsemiText.Format(kind == InspectionKind.Orbit ? "Insp_WarnCloseOrbit" : "Insp_WarnCloseWall",
                    closest, CloseMetres));
            if (Camera != null)
            {
                // the boxes' limits can keep the spacing from giving the overlap asked for
                var d = SurfaceDistance;
                var wantLevel = CameraCoverage.LevelSpacing(Camera, d, (double)sideOverlap.Value / 100) * CurrentState.multiplieralt;
                var wantPhotos = kind == InspectionKind.Orbit
                    ? CameraCoverage.OrbitPhotos(Camera, Distance / CurrentState.multiplierdist,
                        (double)structureRadius.Value / CurrentState.multiplierdist, (double)frontOverlap.Value / 100)
                    : 0;
                if (wantLevel < (double)levelSpacing.Minimum || wantPhotos > (double)photosPerOrbit.Maximum)
                    warnings.Add(FsemiText.Get("Insp_WarnOverlap"));
                if (kind == InspectionKind.Orbit && Distance - (double)structureRadius.Value < CloseMetres * CurrentState.multiplierdist)
                    warnings.Add(FsemiText.Format("Insp_WarnStructure", CloseMetres));
            }

            if (Math.Min(TopAlt, BottomAlt) / CurrentState.multiplieralt < 3)
                warnings.Add(FsemiText.Get("Insp_WarnLow"));
            if (items.Count > ManyItems)
                warnings.Add(FsemiText.Format("Insp_WarnItems", ManyItems));
            warning.Text = string.Join("\n", warnings);
        }

        /// <summary>Waypoint speed and acceleration in m/s and m/s/s: WPNAV_SPEED and WPNAV_ACCEL, or ArduCopter's defaults.</summary>
        public static (double speed, double accel) WaypointSpeed()
        {
            return (Math.Max(0.5, (DeliveryConfig.Param("WPNAV_SPEED") ?? 1000) / 100),
                Math.Max(0.5, (DeliveryConfig.Param("WPNAV_ACCEL") ?? 250) / 100));
        }

        /// <summary>Share of the battery kept in reserve, percent.</summary>
        public const double Reserve = 30;

        /// <summary>
        /// Minutes the vehicle flies on one battery, as set in the dialog (20 until then); whole seconds in the
        /// settings, so they read back in any culture.
        /// </summary>
        public static double EnduranceMinutes
        {
            get => Settings.Instance.GetInt32("inspection_endurance_s", 20 * 60) / 60.0;
            set => Settings.Instance["inspection_endurance_s"] = ((int)Math.Round(value * 60)).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Seconds one flight may take: the battery time less the reserve.</summary>
        public static double FlightBudgetSeconds => EnduranceMinutes * 60 * (1 - Reserve / 100);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (DialogResult != DialogResult.OK)
                return;

            var prefix = "inspection_" + kind.ToString().ToLowerInvariant() + "_";
            Settings.Instance[prefix + "distance"] = Distance.ToString();
            Settings.Instance[prefix + "top"] = TopAlt.ToString();
            Settings.Instance[prefix + "bottom"] = BottomAlt.ToString();
            Settings.Instance[prefix + "level_spacing"] = LevelSpacing.ToString();
            Settings.Instance[prefix + "photos_per_orbit"] = PhotosPerOrbit.ToString();
            Settings.Instance[prefix + "photo_spacing"] = PhotoSpacing.ToString();
            Settings.Instance[prefix + "hover"] = Hover.ToString();
            Settings.Instance[prefix + "photos"] = Photos.ToString();
            Settings.Instance[prefix + "rtl"] = ReturnHome.ToString();
            Settings.Instance["inspection_split"] = Split.ToString();
            Settings.Instance[prefix + "clockwise"] = Clockwise.ToString();
            Settings.Instance[prefix + "structure_radius"] = ((double)structureRadius.Value).ToString();
            Settings.Instance["inspection_front_overlap"] = ((double)frontOverlap.Value).ToString();
            Settings.Instance["inspection_side_overlap"] = ((double)sideOverlap.Value).ToString();
            Settings.Instance["inspection_camera"] = Camera?.Name ?? "";
        }
    }
}
