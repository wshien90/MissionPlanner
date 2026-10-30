using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>How a terrain survey keeps its height above the ground.</summary>
    public enum TerrainHeightMode
    {
        /// <summary>
        /// Waypoints in the terrain frame: the autopilot keeps the height above its own terrain data
        /// (TERRAIN_ENABLE), between the waypoints too.
        /// </summary>
        TerrainFrame,

        /// <summary>
        /// Waypoints above sea level, from the planner's elevation data, with extra waypoints along each line so
        /// the height follows the ground; for autopilots without terrain data.
        /// </summary>
        FromElevation
    }

    /// <summary>A survey line, flown from Start to End with the camera at Pitch degrees (-90 straight down).</summary>
    public class SurveyLine
    {
        public PointLatLngAlt Start, End;
        public double Pitch = -90;
        /// <summary>Metres between photos along this line; 0 for the options' PhotoSpacing.</summary>
        public double PhotoSpacing;
    }

    /// <summary>A terrain survey's settings. Distances and heights in metres, angles in degrees.</summary>
    public class TerrainSurveyOptions
    {
        /// <summary>Height above the ground.</summary>
        public double Height = 60;
        /// <summary>Metres between lines.</summary>
        public double LineSpacing = 30;
        /// <summary>Metres between photos along a line (DO_SET_CAM_TRIGG_DIST).</summary>
        public double PhotoSpacing = 15;
        public TerrainHeightMode Mode = TerrainHeightMode.TerrainFrame;
        /// <summary>FromElevation: at most this far between waypoints along a line.</summary>
        public double FollowStep = 30;
        public bool ReturnToLaunch = true;
    }

    /// <summary>
    /// Photo surveys of hills and mountain ranges for 3D models: parallel lines over a polygon, flown back and forth
    /// at a height above the ground, the camera triggered by distance. A second pass at right angles with the camera
    /// tilted (cross-hatched oblique photos) shows the slopes from all sides, which a straight-down pass alone
    /// doesn't for steep ground.
    /// </summary>
    public static class TerrainSurvey
    {
        const double Radius = 6371000;

        /// <summary>The polygon's area in square metres.</summary>
        public static double Area(IList<PointLatLngAlt> polygon)
        {
            var origin = Centre(polygon);
            var p = polygon.Select(q => ToXY(origin, q)).ToList();
            double area = 0;
            for (var i = 0; i < p.Count; i++)
            {
                var j = (i + 1) % p.Count;
                area += p[i].x * p[j].y - p[j].x * p[i].y;
            }

            return Math.Abs(area) / 2;
        }

        /// <summary>
        /// Lines across the polygon at the angle (degrees from north), spacing metres apart, half a spacing in from
        /// its edges, in flying order: each starts where the last ended, back and forth. A line that crosses a
        /// concave part of the polygon is cut into the pieces inside it.
        /// </summary>
        public static List<SurveyLine> Lines(IList<PointLatLngAlt> polygon, double angle, double spacing,
            double pitch = -90, double photoSpacing = 0)
        {
            if (polygon == null || polygon.Count < 3)
                throw new ArgumentException("A survey area needs at least 3 points.");
            if (spacing <= 0)
                throw new ArgumentException("The line spacing must be more than 0.");

            var origin = Centre(polygon);
            // turn the polygon so the lines run along x
            var turn = (90 - angle) * Math.PI / 180;
            var p = polygon.Select(q => Rotate(ToXY(origin, q), -turn)).ToList();
            var minY = p.Min(q => q.y);
            var maxY = p.Max(q => q.y);

            var lines = new List<SurveyLine>();
            var forward = true;
            for (var y = minY + spacing / 2; y < maxY; y += spacing)
            {
                var xs = new List<double>();
                for (var i = 0; i < p.Count; i++)
                {
                    var a = p[i];
                    var b = p[(i + 1) % p.Count];
                    // half-open, so a vertex on the line counts once
                    if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y))
                        xs.Add(a.x + (y - a.y) / (b.y - a.y) * (b.x - a.x));
                }

                xs.Sort();
                var pieces = new List<(double from, double to)>();
                for (var i = 0; i + 1 < xs.Count; i += 2)
                    if (xs[i + 1] - xs[i] > 0.01)
                        pieces.Add((xs[i], xs[i + 1]));
                if (!forward)
                    pieces.Reverse();
                foreach (var (from, to) in pieces)
                {
                    var s = forward ? from : to;
                    var e = forward ? to : from;
                    lines.Add(new SurveyLine
                    {
                        Start = ToLatLng(origin, Rotate((s, y), turn)), End = ToLatLng(origin, Rotate((e, y), turn)),
                        Pitch = pitch, PhotoSpacing = photoSpacing
                    });
                }

                forward = !forward;
            }

            return lines;
        }

        /// <summary>
        /// The mission items for the lines: each line from start to end with the camera at the line's pitch (set
        /// when it changes), triggered every PhotoSpacing metres along it and stopped at its end. ground(lat, lng) gives the ground's
        /// height above sea level, null where unknown; FromElevation needs it everywhere.
        /// </summary>
        public static List<InspectionItem> Build(IList<SurveyLine> lines,
            TerrainSurveyOptions o, Func<double, double, double?> ground = null)
        {
            if (lines.Count == 0)
                throw new ArgumentException(FsemiText.Get("Terrain_ErrorNoLines"));

            var frame = o.Mode == TerrainHeightMode.TerrainFrame
                ? MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT
                : MAVLink.MAV_FRAME.GLOBAL;
            InspectionItem Wp(PointLatLngAlt at)
            {
                var alt = o.Height;
                if (o.Mode == TerrainHeightMode.FromElevation)
                {
                    var g = ground?.Invoke(at.Lat, at.Lng);
                    if (g == null)
                        throw new ArgumentException(FsemiText.Format("Terrain_ErrorNoElevation", at.Lat, at.Lng));
                    alt = g.Value + o.Height;
                }

                var wp = InspectionItem.Waypoint(at, alt, 0);
                wp.Frame = frame;
                return wp;
            }

            // a structure inspection before it may have left an ROI
            var items = new List<InspectionItem> { InspectionItem.RoiNone() };
            double? pitch = null;
            foreach (var line in lines)
            {
                var start = line.Start;
                var end = line.End;
                items.Add(Wp(start));
                // DO_MOUNT_CONTROL: param1 the pitch; set at the start of the line, so the camera turns on the way
                // there would have to wait for the waypoint anyway
                if (pitch != line.Pitch)
                {
                    items.Add(new InspectionItem { Cmd = MAVLink.MAV_CMD.DO_MOUNT_CONTROL, P1 = line.Pitch });
                    pitch = line.Pitch;
                }

                items.Add(new InspectionItem
                {
                    Cmd = MAVLink.MAV_CMD.DO_SET_CAM_TRIGG_DIST, P1 = line.PhotoSpacing > 0 ? line.PhotoSpacing : o.PhotoSpacing
                });
                if (o.Mode == TerrainHeightMode.FromElevation)
                {
                    // the line in steps of at most FollowStep, so the height above sea level follows the ground
                    var length = start.GetDistance(end);
                    // a little slack, so a line a hair longer than whole steps gets no extra one
                    var steps = (int)Math.Ceiling(length / Math.Max(1, o.FollowStep) - 1e-3);
                    for (var k = 1; k < steps; k++)
                        items.Add(Wp(Between(start, end, (double)k / steps)));
                }

                items.Add(Wp(end));
                items.Add(new InspectionItem { Cmd = MAVLink.MAV_CMD.DO_SET_CAM_TRIGG_DIST, P1 = 0 });
            }

            if (o.ReturnToLaunch)
                items.Add(InspectionItem.ReturnToLaunch());
            return items;
        }

        /// <summary>
        /// The lines as flights that each take no more than budget seconds (from home and back, as
        /// InspectionPattern.MissionSeconds counts them), each built by build(lines) and ending with RTL; a line
        /// that takes longer on its own is a flight by itself. FirstLine and LastLine count from 1.
        /// </summary>
        public static List<InspectionFlight> Flights(IList<SurveyLine> lines,
            Func<IList<SurveyLine>, List<InspectionItem>> build, PointLatLngAlt home,
            double speed, double accel, double budget)
        {
            var flights = new List<InspectionFlight>();
            var first = 0;
            while (first < lines.Count)
            {
                var count = 1;
                while (first + count < lines.Count &&
                       InspectionPattern.MissionSeconds(build(lines.Skip(first).Take(count + 1).ToList()), home, speed,
                           accel) <= budget)
                    count++;
                var items = build(lines.Skip(first).Take(count).ToList());
                flights.Add(new InspectionFlight
                {
                    Items = items, Seconds = InspectionPattern.MissionSeconds(items, home, speed, accel),
                    FirstLine = first + 1, LastLine = first + count
                });
                first += count;
            }

            return flights;
        }

        /// <summary>Lowest and highest ground in the polygon from samples about every step metres, or null without data.</summary>
        public static (double low, double high)? GroundRange(IList<PointLatLngAlt> polygon, double step,
            Func<double, double, double?> ground)
        {
            var heights = new List<double>();
            foreach (var line in Lines(polygon, 0, step))
            {
                var length = line.Start.GetDistance(line.End);
                var n = Math.Max(1, (int)Math.Ceiling(length / step));
                for (var k = 0; k <= n; k++)
                {
                    var at = Between(line.Start, line.End, (double)k / n);
                    var g = ground(at.Lat, at.Lng);
                    if (g != null)
                        heights.Add(g.Value);
                }
            }

            return heights.Count == 0 ? ((double, double)?)null : (heights.Min(), heights.Max());
        }

        static PointLatLngAlt Between(PointLatLngAlt a, PointLatLngAlt b, double t)
        {
            return new PointLatLngAlt(a.Lat + (b.Lat - a.Lat) * t, a.Lng + (b.Lng - a.Lng) * t);
        }

        static PointLatLngAlt Centre(IList<PointLatLngAlt> polygon)
        {
            return new PointLatLngAlt(polygon.Average(p => p.Lat), polygon.Average(p => p.Lng));
        }

        // metres east (x) and north (y) of the origin; flat over a survey's few kilometres
        static (double x, double y) ToXY(PointLatLngAlt origin, PointLatLngAlt p)
        {
            return ((p.Lng - origin.Lng) * Math.PI / 180 * Radius * Math.Cos(origin.Lat * Math.PI / 180),
                (p.Lat - origin.Lat) * Math.PI / 180 * Radius);
        }

        static PointLatLngAlt ToLatLng(PointLatLngAlt origin, (double x, double y) p)
        {
            return new PointLatLngAlt(origin.Lat + p.y / Radius * 180 / Math.PI,
                origin.Lng + p.x / (Radius * Math.Cos(origin.Lat * Math.PI / 180)) * 180 / Math.PI);
        }

        static (double x, double y) Rotate((double x, double y) p, double a)
        {
            return (p.x * Math.Cos(a) - p.y * Math.Sin(a), p.x * Math.Sin(a) + p.y * Math.Cos(a));
        }
    }
}
