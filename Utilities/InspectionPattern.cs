using System;
using System.Collections.Generic;
using System.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>A mission item made by an inspection pattern. Altitudes and distances in metres.</summary>
    public class InspectionItem
    {
        public MAVLink.MAV_CMD Cmd;
        public double P1, P2;
        public double Lat, Lng, Alt;

        /// <summary>True when Lat, Lng and Alt are a position (waypoints and ROI), false for other commands.</summary>
        public bool HasPosition;

        /// <summary>The altitude frame, or null for the planner's (relative to home for the patterns).</summary>
        public MAVLink.MAV_FRAME? Frame;

        public static InspectionItem Waypoint(PointLatLngAlt at, double alt, double hover)
        {
            return new InspectionItem { Cmd = MAVLink.MAV_CMD.WAYPOINT, P1 = hover, Lat = at.Lat, Lng = at.Lng, Alt = alt, HasPosition = true };
        }

        /// <summary>Points the camera (and on a copter the nose) at a position until changed.</summary>
        public static InspectionItem Roi(PointLatLngAlt at, double alt)
        {
            return new InspectionItem { Cmd = MAVLink.MAV_CMD.DO_SET_ROI, Lat = at.Lat, Lng = at.Lng, Alt = alt, HasPosition = true };
        }

        /// <summary>DO_SET_ROI at 0, 0, 0 cancels the ROI.</summary>
        public static InspectionItem RoiNone()
        {
            return new InspectionItem { Cmd = MAVLink.MAV_CMD.DO_SET_ROI };
        }

        /// <summary>DO_DIGICAM_CONTROL with param5 (the Lat column) 1: take one photo.</summary>
        public static InspectionItem Photo()
        {
            return new InspectionItem { Cmd = MAVLink.MAV_CMD.DO_DIGICAM_CONTROL, P1 = 1, Lat = 1 };
        }

        public static InspectionItem ReturnToLaunch()
        {
            return new InspectionItem { Cmd = MAVLink.MAV_CMD.RETURN_TO_LAUNCH };
        }
    }

    /// <summary>One flight of an inspection split by battery: its items, how long it takes, its top and bottom.</summary>
    public class InspectionFlight
    {
        public List<InspectionItem> Items;
        public double Seconds;
        /// <summary>Metres above home.</summary>
        public double TopAlt, BottomAlt;
        /// <summary>A terrain survey's flight: its first and last line, from 1; 0 for an inspection.</summary>
        public int FirstLine, LastLine;
    }

    /// <summary>
    /// Flight patterns for inspecting structures: orbits around a tower or pole and passes along a wall.
    /// Levels are flown from the top down, so the vehicle arrives and leaves at the top altitude. ArduPilot runs
    /// DO commands once the navigation command before them (hover included) is done: a photo after a waypoint is
    /// taken there, and an ROI before a waypoint turns the camera while flying to it.
    /// </summary>
    public static class InspectionPattern
    {
        /// <summary>More waypoints than this don't fit any flight controller's mission storage.</summary>
        public const int MaxWaypoints = 1000;

        /// <summary>Altitudes from top to bottom, both included, evenly spaced no more than spacing apart.</summary>
        public static List<double> Levels(double bottomAlt, double topAlt, double spacing)
        {
            var top = Math.Max(bottomAlt, topAlt);
            var bottom = Math.Min(bottomAlt, topAlt);
            if (spacing <= 0 || top - bottom < 0.01)
                return new List<double> { top };

            var count = (int)Math.Ceiling((top - bottom) / spacing - 1e-9) + 1;
            var step = (top - bottom) / (count - 1);
            return Enumerable.Range(0, count).Select(i => top - i * step).ToList();
        }

        /// <summary>
        /// Orbits around center at each level, the camera on the center at the level's altitude, a photo at each
        /// point. Starts at startBearing (degrees from the center) and ends back above the first point at the top.
        /// </summary>
        public static List<InspectionItem> Orbit(PointLatLngAlt center, double radius, double bottomAlt, double topAlt,
            double levelSpacing, int pointsPerOrbit, double startBearing, bool clockwise, double hover, bool photos,
            bool returnToLaunch)
        {
            return Orbit(center, radius, Levels(bottomAlt, topAlt, levelSpacing), pointsPerOrbit, startBearing,
                clockwise, hover, photos, returnToLaunch);
        }

        /// <summary>The orbits at the given altitudes, in that order: one flight of a split inspection.</summary>
        public static List<InspectionItem> Orbit(PointLatLngAlt center, double radius, IList<double> levels,
            int pointsPerOrbit, double startBearing, bool clockwise, double hover, bool photos, bool returnToLaunch)
        {
            if (radius <= 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (pointsPerOrbit < 3)
                throw new ArgumentOutOfRangeException(nameof(pointsPerOrbit));
            if (levels.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(levels));

            CheckSize(levels.Count * pointsPerOrbit);

            var items = new List<InspectionItem>();
            var step = 360.0 / pointsPerOrbit * (clockwise ? 1 : -1);
            var first = center.newpos(startBearing, radius);

            foreach (var alt in levels)
            {
                items.Add(InspectionItem.Roi(center, alt));
                for (var k = 0; k < pointsPerOrbit; k++)
                {
                    items.Add(InspectionItem.Waypoint(center.newpos(Wrap(startBearing + k * step), radius), alt, hover));
                    if (photos)
                        items.Add(InspectionItem.Photo());
                }
            }

            items.Add(InspectionItem.RoiNone());
            items.Add(InspectionItem.Waypoint(first, levels[0], 0));
            if (returnToLaunch)
                items.Add(InspectionItem.ReturnToLaunch());
            return items;
        }

        /// <summary>
        /// Passes along the wall from wallStart to wallEnd at each level, standoff metres away on the left or right
        /// of the wall (looking from start to end), alternating direction. Before each point the camera is pointed
        /// at the wall straight across from it, and a photo is taken there. Ends at the top altitude.
        /// </summary>
        public static List<InspectionItem> Facade(PointLatLngAlt wallStart, PointLatLngAlt wallEnd, bool leftSide,
            double standoff, double bottomAlt, double topAlt, double levelSpacing, double photoSpacing, double hover,
            bool photos, bool returnToLaunch)
        {
            return Facade(wallStart, wallEnd, leftSide, standoff, Levels(bottomAlt, topAlt, levelSpacing), photoSpacing,
                hover, photos, returnToLaunch);
        }

        /// <summary>The passes at the given altitudes, in that order: one flight of a split inspection.</summary>
        public static List<InspectionItem> Facade(PointLatLngAlt wallStart, PointLatLngAlt wallEnd, bool leftSide,
            double standoff, IList<double> levels, double photoSpacing, double hover, bool photos, bool returnToLaunch)
        {
            if (levels.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(levels));
            if (standoff <= 0)
                throw new ArgumentOutOfRangeException(nameof(standoff));
            if (photoSpacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(photoSpacing));

            var length = wallStart.GetDistance(wallEnd);
            if (length < 1)
                throw new ArgumentException(FsemiText.Get("Insp_ErrorWall"));

            var bearing = wallStart.GetBearing(wallEnd);
            var away = Wrap(bearing + (leftSide ? -90 : 90));
            var count = Math.Max(2, (int)Math.Ceiling(length / photoSpacing - 1e-9) + 1);
            CheckSize(levels.Count * (double)count);
            var wall = Enumerable.Range(0, count).Select(j => wallStart.newpos(bearing, j * length / (count - 1))).ToList();

            var items = new List<InspectionItem>();
            for (var l = 0; l < levels.Count; l++)
            {
                var alt = levels[l];
                var order = l % 2 == 0 ? wall : Enumerable.Reverse(wall).ToList();
                foreach (var target in order)
                {
                    items.Add(InspectionItem.Roi(target, alt));
                    items.Add(InspectionItem.Waypoint(target.newpos(away, standoff), alt, hover));
                    if (photos)
                        items.Add(InspectionItem.Photo());
                }
            }

            // climb where the last pass ended
            var last = (levels.Count % 2 == 1 ? wall.Last() : wall.First()).newpos(away, standoff);
            items.Add(InspectionItem.RoiNone());
            items.Add(InspectionItem.Waypoint(last, levels[0], 0));
            if (returnToLaunch)
                items.Add(InspectionItem.ReturnToLaunch());
            return items;
        }

        /// <summary>
        /// Splits the levels, in order, into flights that each take no more than budget seconds, by seconds(levels)
        /// for a flight of those levels; a level that takes longer on its own is a flight by itself.
        /// </summary>
        public static List<List<double>> SplitLevels(IList<double> levels, Func<IList<double>, double> seconds,
            double budget)
        {
            var flights = new List<List<double>>();
            var current = new List<double>();
            foreach (var level in levels)
            {
                var longer = new List<double>(current) { level };
                if (current.Count > 0 && seconds(longer) > budget)
                {
                    flights.Add(current);
                    current = new List<double> { level };
                }
                else
                    current = longer;
            }

            if (current.Count > 0)
                flights.Add(current);
            return flights;
        }

        /// <summary>
        /// Seconds a flight of the items takes from take-off at home to landing back: the pattern (FlightTime),
        /// the legs from home to its first waypoint and from its last back, and take-off and landing. Without a
        /// home only the pattern and take-off and landing count.
        /// </summary>
        public static double MissionSeconds(IEnumerable<InspectionItem> items, PointLatLngAlt home, double speed,
            double accel)
        {
            var list = items.ToList();
            var total = FlightTime(list, speed, accel) + TakeoffLandingSeconds;
            var points = list.Where(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT).ToList();
            if (home != null && points.Count > 0)
            {
                var first = new PointLatLngAlt(points.First().Lat, points.First().Lng);
                var last = new PointLatLngAlt(points.Last().Lat, points.Last().Lng);
                total += (home.GetDistance(first) + last.GetDistance(home)) / Math.Max(speed, 0.5);
            }

            return total;
        }

        /// <summary>
        /// The inspection as flights of whole levels that each fit budget seconds, each made by build(levels) and
        /// timed from home and back.
        /// </summary>
        public static List<InspectionFlight> Flights(IList<double> levels, Func<IList<double>, List<InspectionItem>> build,
            PointLatLngAlt home, double speed, double accel, double budget)
        {
            return SplitLevels(levels, l => MissionSeconds(build(l), home, speed, accel), budget)
                .Select(l =>
                {
                    var items = build(l);
                    return new InspectionFlight
                    {
                        Items = items, Seconds = MissionSeconds(items, home, speed, accel), TopAlt = l.Max(),
                        BottomAlt = l.Min()
                    };
                }).ToList();
        }

        /// <summary>Seconds for take-off, climb and landing.</summary>
        public const double TakeoffLandingSeconds = 2 * 60;

        static void CheckSize(double waypoints)
        {
            if (waypoints > MaxWaypoints)
                throw new ArgumentException(FsemiText.Format("Insp_ErrorSize", waypoints, MaxWaypoints));
        }

        /// <summary>
        /// How close an orbit comes to its center: the vehicle flies straight between points, so the middle of each
        /// leg is radius * cos(180 / points) from the center.
        /// </summary>
        public static double ClosestApproach(double radius, int pointsPerOrbit)
        {
            return radius * Math.Cos(Math.PI / pointsPerOrbit);
        }

        /// <summary>
        /// Whether the vehicle flies to the command's position. ROI, DO_ORBIT and the like carry a position the
        /// vehicle doesn't go to.
        /// </summary>
        public static bool IsFlyTo(ushort id)
        {
            switch ((MAVLink.MAV_CMD)id)
            {
                case MAVLink.MAV_CMD.WAYPOINT:
                case MAVLink.MAV_CMD.SPLINE_WAYPOINT:
                case MAVLink.MAV_CMD.LOITER_UNLIM:
                case MAVLink.MAV_CMD.LOITER_TURNS:
                case MAVLink.MAV_CMD.LOITER_TIME:
                case MAVLink.MAV_CMD.LOITER_TO_ALT:
                case MAVLink.MAV_CMD.LAND:
                case MAVLink.MAV_CMD.PAYLOAD_PLACE:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether point is on the left of the line from a to b.</summary>
        public static bool IsLeftOf(PointLatLngAlt a, PointLatLngAlt b, PointLatLngAlt point)
        {
            return SideOf(a, b, point) > 0;
        }

        /// <summary>
        /// Distance in metres of point from the line through a and b: positive on the left looking from a to b,
        /// negative on the right. A flat local approximation, fine over a few kilometres.
        /// </summary>
        public static double SideOf(PointLatLngAlt a, PointLatLngAlt b, PointLatLngAlt point)
        {
            const double metresPerDegree = 111320;
            var cos = Math.Cos(a.Lat * MathHelper.deg2rad);
            var bx = (b.Lng - a.Lng) * cos * metresPerDegree;
            var by = (b.Lat - a.Lat) * metresPerDegree;
            var px = (point.Lng - a.Lng) * cos * metresPerDegree;
            var py = (point.Lat - a.Lat) * metresPerDegree;
            var length = Math.Sqrt(bx * bx + by * by);
            return length == 0 ? 0 : (bx * py - by * px) / length;
        }

        /// <summary>Each waypoint with the length in metres of the leg to it (0 for the first), climbs included.</summary>
        static IEnumerable<(InspectionItem wp, double leg)> Legs(IEnumerable<InspectionItem> items)
        {
            InspectionItem previous = null;
            foreach (var wp in items.Where(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT))
            {
                double leg = 0;
                if (previous != null)
                {
                    var flat = new PointLatLngAlt(previous.Lat, previous.Lng).GetDistance(new PointLatLngAlt(wp.Lat, wp.Lng));
                    leg = Math.Sqrt(flat * flat + Math.Pow(wp.Alt - previous.Alt, 2));
                }

                yield return (wp, leg);
                previous = wp;
            }
        }

        /// <summary>Length in metres of the path through the waypoints, including climbs and descents.</summary>
        public static double PathLength(IEnumerable<InspectionItem> items)
        {
            return Legs(items).Sum(l => l.leg);
        }

        /// <summary>Extra seconds a copter takes at each waypoint it stops at (jerk-limited stop and settle),
        /// measured on ArduCopter 4.5 SITL.</summary>
        public const double StopOverhead = 3;

        /// <summary>
        /// Estimated seconds through the waypoints: each leg accelerates at accel up to speed and stops at the
        /// waypoint (as it does with a hover), plus the hover and StopOverhead at each one. Speed in m/s, accel in m/s/s.
        /// </summary>
        public static double FlightTime(IEnumerable<InspectionItem> items, double speed, double accel)
        {
            double total = 0;
            foreach (var (wp, leg) in Legs(items))
            {
                // trapezoid when the leg is long enough to reach speed, triangle otherwise
                if (leg > 0)
                    total += leg >= speed * speed / accel ? leg / speed + speed / accel : 2 * Math.Sqrt(leg / accel);
                total += wp.P1 + StopOverhead;
            }

            return total;
        }

        static double Wrap(double bearing)
        {
            bearing %= 360;
            return bearing < 0 ? bearing + 360 : bearing;
        }
    }
}
