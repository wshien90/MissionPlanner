using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews
{
    /// <summary>
    /// FSEMI structure inspection in the flight planner, under Inspection on the map menu: an orbit around a
    /// structure at the clicked point, or passes along a wall picked with two clicks.
    /// </summary>
    public partial class FlightPlanner
    {
        GMapOverlay inspectionoverlay;
        ToolStripMenuItem facadeEndMenuItem;
        PointLatLngAlt facadeStart;

        // an inspection split by battery: its flights, the mission row it starts at and the flight there now
        ToolStripMenuItem flightsMenuItem;
        List<InspectionFlight> flights;
        int flightsStart, currentFlight;
        // where its flights were saved, a file each; null when they couldn't be
        string flightsFolder;

        void InitInspection()
        {
            var menu = new ToolStripMenuItem(FsemiText.Get("Plan_Inspection"));
            var orbit = new ToolStripMenuItem(FsemiText.Get("Plan_Orbit"));
            orbit.Click += inspectionOrbitToolStripMenuItem_Click;
            var start = new ToolStripMenuItem(FsemiText.Get("Plan_FacadeStart"));
            start.Click += facadeStartToolStripMenuItem_Click;
            facadeEndMenuItem = new ToolStripMenuItem(FsemiText.Get("Plan_FacadeEnd")) { Enabled = false };
            facadeEndMenuItem.Click += facadeEndToolStripMenuItem_Click;
            flightsMenuItem = new ToolStripMenuItem(FsemiText.Get("Plan_Flights")) { Visible = false };
            menu.DropDownItems.AddRange(new ToolStripItem[] { orbit, start, facadeEndMenuItem, TerrainMenuItem(), flightsMenuItem });
            contextMenuStrip1.Items.Insert(contextMenuStrip1.Items.IndexOf(setROIToolStripMenuItem) + 1, menu);

            inspectionoverlay = new GMapOverlay("inspection");
            MainMap.Overlays.Add(inspectionoverlay);
        }

        /// <summary>The planned home, or null when it isn't set.</summary>
        PointLatLngAlt PlannedHome()
        {
            if (double.TryParse(TXT_homelat.Text, out var lat) && double.TryParse(TXT_homelng.Text, out var lng) &&
                (lat != 0 || lng != 0))
                return new PointLatLngAlt(lat, lng);
            return null;
        }

        /// <summary>
        /// Where the vehicle comes from into a pattern added to the end of the mission: the last position the
        /// mission flies to, or home when the mission is empty or ends with RTL. Null when neither is known.
        /// </summary>
        PointLatLngAlt ApproachPoint(out string name)
        {
            try
            {
                var commands = GetCommandList();
                for (var i = commands.Count - 1; i >= 0; i--)
                {
                    if (commands[i].id == (ushort)MAVLink.MAV_CMD.RETURN_TO_LAUNCH)
                        break;
                    if (InspectionPattern.IsFlyTo(commands[i].id) && (commands[i].lat != 0 || commands[i].lng != 0))
                    {
                        name = FsemiText.Format("Plan_FromItem", i + 1);
                        return new PointLatLngAlt(commands[i].lat, commands[i].lng);
                    }
                }
            }
            catch (FormatException)
            {
                // a row with an invalid number: use home
            }

            name = FsemiText.Get("Plan_FromHome");
            return PlannedHome();
        }

        void inspectionOrbitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!CheckMissionType())
                return;

            var center = new PointLatLngAlt(MouseDownStart.Lat, MouseDownStart.Lng);
            // start on the side the vehicle comes from, so the leg in doesn't cross the structure
            var approach = ApproachPoint(out var from);
            string approachText;
            double startBearing;
            if (approach != null && approach.GetDistance(center) > 1)
            {
                startBearing = center.GetBearing(approach);
                approachText = FsemiText.Format("Plan_OrbitApproach", from);
            }
            else
            {
                startBearing = 0;
                approachText = FsemiText.Get("Plan_OrbitNoApproach");
            }

            List<InspectionItem> BuildLevels(InspectionForm f, IList<double> levels, bool rtl) => InspectionPattern.Orbit(
                center, f.Distance / CurrentState.multiplierdist, levels, f.PhotosPerOrbit, startBearing, f.Clockwise,
                f.Hover, f.Photos, rtl);
            List<InspectionItem> Build(InspectionForm f) => BuildLevels(f, Levels(f), f.ReturnHome);
            List<InspectionFlight> Flights(InspectionForm f) => SplitFlights(Levels(f), l => BuildLevels(f, l, true));

            using (var form = new InspectionForm(InspectionKind.Orbit, DefaultAltitude(), Build, Flights, approachText))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    AddInspection(form, Build, Flights);
            }
        }

        void facadeStartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            facadeStart = new PointLatLngAlt(MouseDownStart.Lat, MouseDownStart.Lng);
            facadeEndMenuItem.Enabled = true;

            inspectionoverlay.Markers.Clear();
            inspectionoverlay.Routes.Clear();
            inspectionoverlay.Markers.Add(new GMarkerGoogle(facadeStart, GMarkerGoogleType.yellow_dot)
            {
                ToolTipText = FsemiText.Get("Plan_WallStartTip"),
                ToolTipMode = MarkerTooltipMode.Always
            });
        }

        void facadeEndToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (facadeStart == null || !CheckMissionType())
                return;

            var start = facadeStart;
            var end = new PointLatLngAlt(MouseDownStart.Lat, MouseDownStart.Lng);

            inspectionoverlay.Markers.Add(new GMarkerGoogle(end, GMarkerGoogleType.yellow_dot)
                { ToolTipText = FsemiText.Get("Plan_WallEnd") });
            inspectionoverlay.Routes.Add(new GMapRoute(new List<PointLatLng> { start, end }, "wall")
                { Stroke = new System.Drawing.Pen(System.Drawing.Color.Yellow, 3) });

            var approach = ApproachPoint(out var from);
            // begin at the wall end nearer the vehicle, so the leg in doesn't run along the wall
            if (approach != null && approach.GetDistance(end) < approach.GetDistance(start))
            {
                var swap = start;
                start = end;
                end = swap;
            }

            // first side choice: the side the vehicle comes from when it is clear of the wall line, else the left
            string[] sides;
            bool firstIsLeft;
            string approachText;
            if (approach != null && Math.Abs(InspectionPattern.SideOf(start, end, approach)) > 1)
            {
                firstIsLeft = InspectionPattern.SideOf(start, end, approach) > 0;
                sides = new[] { FsemiText.Get("Plan_SideApproach"), FsemiText.Get("Plan_SideFar") };
                approachText = FsemiText.Format("Plan_FacadeApproach", from);
            }
            else
            {
                firstIsLeft = true;
                sides = new[] { FsemiText.Get("Plan_SideLeft"), FsemiText.Get("Plan_SideRight") };
                approachText = approach == null
                    ? FsemiText.Get("Plan_FacadeNoApproach")
                    : FsemiText.Format("Plan_FacadeInLine", from);
            }

            List<InspectionItem> BuildLevels(InspectionForm f, IList<double> levels, bool rtl) => InspectionPattern.Facade(
                start, end, f.FirstSide ? firstIsLeft : !firstIsLeft, f.Distance / CurrentState.multiplierdist, levels,
                f.PhotoSpacing / CurrentState.multiplierdist, f.Hover, f.Photos, rtl);
            List<InspectionItem> Build(InspectionForm f) => BuildLevels(f, Levels(f), f.ReturnHome);
            List<InspectionFlight> Flights(InspectionForm f) => SplitFlights(Levels(f), l => BuildLevels(f, l, true));

            var wallText = FsemiText.Format("Plan_Wall", start.GetDistance(end) *
                CurrentState.multiplierdist, CurrentState.DistanceUnit, start.GetBearing(end));
            using (var form = new InspectionForm(InspectionKind.Facade, DefaultAltitude(), Build, Flights, approachText,
                       wallText, sides))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    AddInspection(form, Build, Flights);
            }

            facadeStart = null;
            facadeEndMenuItem.Enabled = false;
            inspectionoverlay.Markers.Clear();
            inspectionoverlay.Routes.Clear();
        }

        /// <summary>The form's levels in metres above home, top down.</summary>
        static List<double> Levels(InspectionForm f)
        {
            return InspectionPattern.Levels(f.BottomAlt / CurrentState.multiplieralt, f.TopAlt / CurrentState.multiplieralt,
                f.LevelSpacing / CurrentState.multiplieralt);
        }

        /// <summary>The levels as flights that each fit the battery, timed from the planned home.</summary>
        List<InspectionFlight> SplitFlights(IList<double> levels, Func<IList<double>, List<InspectionItem>> build)
        {
            var (speed, accel) = InspectionForm.WaypointSpeed();
            return InspectionPattern.Flights(levels, build, PlannedHome(), speed, accel, InspectionForm.FlightBudgetSeconds);
        }

        /// <summary>
        /// Adds the pattern to the mission, or when split into flights the first one, keeping the others under
        /// Inspection flights on the map menu.
        /// </summary>
        void AddInspection(InspectionForm form, Func<InspectionForm, List<InspectionItem>> build,
            Func<InspectionForm, List<InspectionFlight>> split)
        {
            var list = form.Split ? split(form) : null;
            if (list == null || list.Count <= 1)
                AddInspection(list?.FirstOrDefault()?.Items ?? build(form));
            else
                AddFlights(list);
        }

        /// <summary>
        /// Adds the first of several flights to the mission, saves each to a file and lists them all under
        /// Inspection flights on the map menu.
        /// </summary>
        void AddFlights(List<InspectionFlight> list)
        {
            flights = list;
            flightsStart = Commands.Rows.Count;
            currentFlight = 0;
            flightsFolder = SaveFlights(list);
            AddInspection(list[0].Items);
            UpdateFlightsMenu();
            var text = FsemiText.Format("Plan_FlightsAdded", list.Count) + "\n\n" + (flightsFolder != null
                ? FsemiText.Format("Plan_FlightsSaved", flightsFolder)
                : FsemiText.Get("Plan_FlightsNotSaved"));
            CustomMessageBox.Show(text, FsemiText.Get("Plan_Flights"));
        }

        /// <summary>
        /// Saves each flight, after the mission's rows so far, to a .waypoints file in a new folder under the user's
        /// missions folder; returns the folder, or null when it couldn't be written.
        /// </summary>
        string SaveFlights(List<InspectionFlight> list)
        {
            try
            {
                var en = CultureInfo.InvariantCulture;
                var before = new List<Locationwp>();
                // as the planner's Save writes the rows: the cells as shown, altitudes back to metres
                for (var a = 0; a < Commands.Rows.Count; a++)
                {
                    var cells = Commands.Rows[a].Cells;
                    double Cell(int i) => double.Parse(cells[i].Value.ToString());
                    before.Add(new Locationwp
                    {
                        id = cells[Command.Index].Value.ToString() == "UNKNOWN"
                            ? (ushort)cells[Command.Index].Tag
                            : getCmdID(cells[Command.Index].Value.ToString()),
                        frame = (byte)(int)cells[Frame.Index].Value,
                        p1 = (float)Cell(Param1.Index), p2 = (float)Cell(Param2.Index), p3 = (float)Cell(Param3.Index),
                        p4 = (float)Cell(Param4.Index), lat = Cell(Lat.Index), lng = Cell(Lon.Index),
                        alt = (float)(Cell(Alt.Index) / CurrentState.multiplieralt)
                    });
                }

                var home = PlannedHome();
                if (home != null && double.TryParse(TXT_homealt.Text, out var homeAlt))
                    home.Alt = homeAlt / CurrentState.multiplieralt;
                var folder = Path.Combine(InspectionFlightFiles.Root, InspectionFlightFiles.FolderName(DateTime.Now));
                InspectionFlightFiles.Save(folder, home, before, list, (byte)(int)CMB_altmode.SelectedValue);
                return folder;
            }
            catch (Exception ex)
            {
                log.Error("saving the inspection flights", ex);
                return null;
            }
        }

        void UpdateFlightsMenu()
        {
            flightsMenuItem.DropDownItems.Clear();
            flightsMenuItem.Visible = flights != null;
            if (flights == null)
                return;
            for (var i = 0; i < flights.Count; i++)
            {
                var k = i;
                var f = flights[i];
                var text = f.FirstLine > 0
                    ? FsemiText.Format("Plan_FlightItemLines", i + 1, flights.Count, f.FirstLine, f.LastLine, f.Seconds / 60)
                    : FsemiText.Format("Plan_FlightItem", i + 1, flights.Count, f.TopAlt * CurrentState.multiplieralt,
                        f.BottomAlt * CurrentState.multiplieralt, CurrentState.AltUnit, f.Seconds / 60);
                var item = new ToolStripMenuItem(text) { Checked = i == currentFlight };
                item.Click += (s, e) => LoadFlight(k);
                flightsMenuItem.DropDownItems.Add(item);
            }

            if (flightsFolder == null)
                return;
            var open = new ToolStripMenuItem(FsemiText.Get("Plan_FlightsOpenFolder"));
            var folder = flightsFolder;
            open.Click += (s, e) =>
            {
                try
                {
                    Process.Start(folder);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(ex.Message, Strings.ERROR);
                }
            };
            flightsMenuItem.DropDownItems.Add(new ToolStripSeparator());
            flightsMenuItem.DropDownItems.Add(open);
        }

        /// <summary>Replaces the inspection flight in the mission, from the row it started at, with flight k.</summary>
        void LoadFlight(int k)
        {
            if (flights == null)
                return;
            if (Commands.Rows.Count < flightsStart + 1)
            {
                CustomMessageBox.Show(FsemiText.Get("Plan_FlightsGone"), FsemiText.Get("Plan_Flights"));
                flights = null;
                UpdateFlightsMenu();
                return;
            }

            var unchanged = Commands.Rows.Count == flightsStart + flights[currentFlight].Items.Count;
            if (CustomMessageBox.Show(FsemiText.Format(unchanged ? "Plan_FlightReplace" : "Plan_FlightReplaceChanged",
                        k + 1, flights.Count, flightsStart + 1), FsemiText.Get("Plan_Flights"),
                    CustomMessageBox.MessageBoxButtons.YesNo) != CustomMessageBox.DialogResult.Yes)
                return;

            updateUndoBuffer(true);
            // mono fix, as in processToScreen
            Commands.CurrentCell = null;
            while (Commands.Rows.Count > flightsStart)
                Commands.Rows.RemoveAt(Commands.Rows.Count - 1);
            AddInspection(flights[k].Items);
            currentFlight = k;
            UpdateFlightsMenu();
        }

        void AddInspection(List<InspectionItem> items)
        {
            updateUndoBuffer(true);
            // like the other generators: every cell is set here, so skip the per-row defaults and redraws
            quickadd = true;
            try
            {
                foreach (var item in items)
                {
                    AddMissionItem(item.Cmd, item.P1, item.P2, item.Lat, item.Lng,
                        item.HasPosition ? item.Alt * CurrentState.multiplieralt : item.Alt, item.HasPosition);
                    if (item.Frame.HasValue)
                        Commands.Rows[selectedrow].Cells[Frame.Index].Value = (int)item.Frame.Value;
                }
            }
            finally
            {
                quickadd = false;
            }

            writeKML();
        }
    }
}
