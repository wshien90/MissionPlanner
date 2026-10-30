using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews
{
    /// <summary>
    /// FSEMI terrain survey in the flight planner, under Inspection on the map menu: photo lines over the drawn
    /// polygon at a height above the ground, for 3D models of hills and mountain ranges.
    /// </summary>
    public partial class FlightPlanner
    {
        ToolStripMenuItem TerrainMenuItem()
        {
            var item = new ToolStripMenuItem(FsemiText.Get("Terrain_Menu"));
            item.Click += terrainSurveyToolStripMenuItem_Click;
            return item;
        }

        /// <summary>The ground's height above sea level from the planner's elevation data, null where it has none.</summary>
        static double? GroundElevation(double lat, double lng)
        {
            var alt = srtm.getAltitude(lat, lng);
            return alt.currenttype == srtm.tiletype.valid ? alt.alt : (double?)null;
        }

        void terrainSurveyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!CheckMissionType())
                return;

            var points = drawnpolygon?.Points;
            if (points == null || points.Count < 3)
            {
                CustomMessageBox.Show(FsemiText.Get("Terrain_NeedPolygon"), FsemiText.Get("Terrain_Title"));
                return;
            }

            var polygon = points.Select(p => new PointLatLngAlt(p.Lat, p.Lng)).ToList();
            using (var form = new TerrainSurveyForm(polygon, PlannedHome(), GroundElevation, LongestEdge(polygon)))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;

                var list = form.Split ? form.Flights() : null;
                if (list == null || list.Count <= 1)
                    AddInspection(list?.FirstOrDefault()?.Items ?? form.Build(form.Lines()));
                else
                    AddFlights(list);
            }
        }

        /// <summary>The direction of the polygon's longest edge, degrees from north: lines along it turn least.</summary>
        static double LongestEdge(IList<PointLatLngAlt> polygon)
        {
            var best = 0.0;
            var bearing = 0.0;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var d = a.GetDistance(b);
                if (d > best)
                {
                    best = d;
                    bearing = a.GetBearing(b);
                }
            }

            return (bearing % 180 + 180) % 180;
        }
    }
}
