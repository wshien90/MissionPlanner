using System;
using System.Collections.Generic;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews
{
    /// <summary>
    /// FSEMI delivery in the flight planner: "Delivery drop here" on the map menu adds a drop sequence to the
    /// mission, and the points where the mission releases the payload are marked on the map.
    /// </summary>
    public partial class FlightPlanner
    {
        GMapOverlay deliveryoverlay;

        void InitDelivery()
        {
            var item = new ToolStripMenuItem(FsemiText.Get("Plan_DropHere"));
            item.Click += deliveryDropToolStripMenuItem_Click;
            contextMenuStrip1.Items.Insert(contextMenuStrip1.Items.IndexOf(setROIToolStripMenuItem) + 1, item);

            deliveryoverlay = new GMapOverlay("delivery");
            MainMap.Overlays.Add(deliveryoverlay);
        }

        /// <summary>Whether the planner shows the mission (not fence or rally); tells the user otherwise.</summary>
        bool CheckMissionType()
        {
            if ((MAVLink.MAV_MISSION_TYPE)cmb_missiontype.SelectedValue == MAVLink.MAV_MISSION_TYPE.MISSION)
                return true;
            CustomMessageBox.Show(FsemiText.Get("Plan_MissionOnly"), "FSEMI Mission Planner");
            return false;
        }

        /// <summary>The planner's default altitude, in display units.</summary>
        double DefaultAltitude()
        {
            return double.TryParse(TXT_DefaultAlt.Text, out var alt) ? alt : 50 * CurrentState.multiplieralt;
        }

        void deliveryDropToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!CheckMissionType())
                return;

            var point = MouseDownStart;
            using (var form = new DeliveryDropForm(DefaultAltitude()))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                AddDeliveryDrop(point.Lat, point.Lng, form);
            }
        }

        /// <summary>
        /// Appends: fly to the point at cruise altitude, descend to drop altitude and hover, release, hover,
        /// climb back, then optionally lock the hook and return to launch. ArduPilot runs a DO command once the
        /// navigation command before it is done (including its hover), so the release comes after the first hover
        /// and the second hover keeps the vehicle over the point while the hook opens.
        /// </summary>
        void AddDeliveryDrop(double lat, double lng, DeliveryDropForm form)
        {
            updateUndoBuffer(true);

            var release = DeliveryConfig.Command(form.Method, true, DeliveryConfig.Servo, DeliveryConfig.LockPwm,
                DeliveryConfig.ReleasePwm);
            var lockHook = DeliveryConfig.Command(form.Method, false, DeliveryConfig.Servo, DeliveryConfig.LockPwm,
                DeliveryConfig.ReleasePwm);

            AddMissionItem(MAVLink.MAV_CMD.WAYPOINT, 0, 0, lat, lng, form.CruiseAlt);
            AddMissionItem(MAVLink.MAV_CMD.WAYPOINT, form.HoverBefore, 0, lat, lng, form.DropAlt);
            AddMissionItem(release.cmd, release.p1, release.p2, 0, 0, 0);
            AddMissionItem(MAVLink.MAV_CMD.WAYPOINT, form.HoverAfter, 0, lat, lng, form.DropAlt);
            AddMissionItem(MAVLink.MAV_CMD.WAYPOINT, 0, 0, lat, lng, form.CruiseAlt);
            if (form.LockAfter)
                AddMissionItem(lockHook.cmd, lockHook.p1, lockHook.p2, 0, 0, 0);
            if (form.ReturnHome)
                AddMissionItem(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, 0, 0, 0, 0, 0);

            writeKML();
        }

        /// <summary>
        /// Adds a row with exactly these values. Unlike AddCommand, the altitude is not replaced by the default or
        /// terrain-adjusted altitude, and a waypoint stays a waypoint in spline mode. Altitude in display units.
        /// hasPosition is false for commands that use the Lat and Lon columns for something else.
        /// </summary>
        void AddMissionItem(MAVLink.MAV_CMD cmd, double p1, double p2, double lat, double lng, double alt,
            bool hasPosition = true)
        {
            selectedrow = Commands.Rows.Add();
            var row = Commands.Rows[selectedrow];
            row.Cells[Command.Index].Value = cmd.ToString();
            ChangeColumnHeader(cmd.ToString());
            row.Cells[Param1.Index].Value = p1;
            row.Cells[Param2.Index].Value = p2;
            row.Cells[Param3.Index].Value = 0;
            row.Cells[Param4.Index].Value = 0;
            row.Cells[Lat.Index].Value = lat;
            row.Cells[Lon.Index].Value = lng;
            row.Cells[Alt.Index].Value = alt;
            if (hasPosition && (lat != 0 || lng != 0))
                convertFromGeographic(lat, lng);
        }

        /// <summary>Marks where the mission releases the payload: the position of the navigation command before each release.</summary>
        void ShowDeliveryDrops(List<Locationwp> commands)
        {
            if (deliveryoverlay == null)
                return;

            deliveryoverlay.Markers.Clear();

            Locationwp? lastPosition = null;
            var drop = 0;
            for (var i = 0; i < commands.Count; i++)
            {
                var item = commands[i];
                // only positions the vehicle flies to: an ROI has one it doesn't
                if (InspectionPattern.IsFlyTo(item.id) && (item.lat != 0 || item.lng != 0))
                    lastPosition = item;

                if (!DeliveryConfig.IsRelease(item.id, item.p1, item.p2) || lastPosition == null)
                    continue;

                drop++;
                var at = lastPosition.Value;
                deliveryoverlay.Markers.Add(new GMarkerGoogle(new PointLatLng(at.lat, at.lng), GMarkerGoogleType.orange_dot)
                {
                    ToolTipText = FsemiText.Format("Plan_DropMarker", drop, i + 1,
                        at.alt * CurrentState.multiplieralt, CurrentState.AltUnit),
                    ToolTipMode = MarkerTooltipMode.OnMouseOver
                });
            }
        }
    }
}
