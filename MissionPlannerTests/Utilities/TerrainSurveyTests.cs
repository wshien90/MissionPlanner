using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MissionPlanner.Utilities.Tests
{
    [TestClass]
    public class TerrainSurveyTests
    {
        const double Lat = 21.0285, Lng = 105.8542;

        /// <summary>A position x metres east and y metres north of the origin.</summary>
        static PointLatLngAlt At(double x, double y)
        {
            return new PointLatLngAlt(Lat + y / 111194.9, Lng + x / (111194.9 * Math.Cos(Lat * Math.PI / 180)));
        }

        static readonly List<PointLatLngAlt> Square = new List<PointLatLngAlt>
            { At(0, 0), At(200, 0), At(200, 200), At(0, 200) };

        [TestMethod]
        public void AreaOfASquare()
        {
            Assert.AreEqual(40000, TerrainSurvey.Area(Square), 40);
        }

        [TestMethod]
        public void LinesBackAndForthInsideTheArea()
        {
            // north-south lines 50 m apart, half a spacing in from the edges: x = 25, 75, 125, 175
            var lines = TerrainSurvey.Lines(Square, 0, 50);
            Assert.AreEqual(4, lines.Count);
            foreach (var l in lines)
                Assert.AreEqual(200, l.Start.GetDistance(l.End), 1);
            // the first goes north, the next south, starting where the last ended
            Assert.IsTrue(lines[0].End.Lat > lines[0].Start.Lat);
            Assert.IsTrue(lines[1].End.Lat < lines[1].Start.Lat);
            Assert.AreEqual(50, lines[0].End.GetDistance(lines[1].Start), 1);
            // the outer lines half a spacing in from the edges
            var fromWest = At(0, 0).GetDistance(new PointLatLngAlt(Lat, lines[0].Start.Lng));
            Assert.AreEqual(25, Math.Min(fromWest, 200 - fromWest), 1);

            // east-west at 90 degrees
            var across = TerrainSurvey.Lines(Square, 90, 50);
            Assert.AreEqual(4, across.Count);
            Assert.AreEqual(90, across[0].Start.GetBearing(across[0].End), 1);
            Assert.AreEqual(270, across[1].Start.GetBearing(across[1].End), 1);
        }

        [TestMethod]
        public void ConcaveAreasAreCutIntoPieces()
        {
            // a U open to the north: lines through the arms come in two pieces, those through the base in one
            var u = new List<PointLatLngAlt>
                { At(0, 0), At(300, 0), At(300, 300), At(200, 300), At(200, 100), At(100, 100), At(100, 300), At(0, 300) };
            var lines = TerrainSurvey.Lines(u, 90, 50);
            Assert.AreEqual(2 + 2 * 4, lines.Count);
            Assert.AreEqual(2, lines.Count(l => Math.Abs(l.Start.GetDistance(l.End) - 300) < 1));
            Assert.AreEqual(8, lines.Count(l => Math.Abs(l.Start.GetDistance(l.End) - 100) < 1));
        }

        [TestMethod]
        public void TerrainFrameMission()
        {
            var lines = TerrainSurvey.Lines(Square, 0, 50, -90, 20);
            var items = TerrainSurvey.Build(lines, new TerrainSurveyOptions { Height = 60 });
            var waypoints = items.Where(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT).ToList();
            Assert.AreEqual(8, waypoints.Count);
            Assert.IsTrue(waypoints.All(w => w.Alt == 60 && w.Frame == MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT));
            // camera down once, triggered every 20 m along each line and stopped at its end
            Assert.AreEqual(1, items.Count(i => i.Cmd == MAVLink.MAV_CMD.DO_MOUNT_CONTROL && i.P1 == -90));
            Assert.AreEqual(4, items.Count(i => i.Cmd == MAVLink.MAV_CMD.DO_SET_CAM_TRIGG_DIST && i.P1 == 20));
            Assert.AreEqual(4, items.Count(i => i.Cmd == MAVLink.MAV_CMD.DO_SET_CAM_TRIGG_DIST && i.P1 == 0));
            Assert.AreEqual(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, items.Last().Cmd);
            // the trigger starts after the line's first waypoint and stops after its last
            var first = items.FindIndex(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT);
            Assert.AreEqual(MAVLink.MAV_CMD.DO_SET_CAM_TRIGG_DIST, items[first + 2].Cmd);
            Assert.AreEqual(MAVLink.MAV_CMD.DO_SET_CAM_TRIGG_DIST, items[first + 4].Cmd);
            Assert.AreEqual(0, items[first + 4].P1);
        }

        [TestMethod]
        public void ObliquePassTiltsTheCamera()
        {
            var lines = TerrainSurvey.Lines(Square, 0, 50);
            lines.AddRange(TerrainSurvey.Lines(Square, 90, 50, -45));
            var items = TerrainSurvey.Build(lines, new TerrainSurveyOptions());
            var mounts = items.Where(i => i.Cmd == MAVLink.MAV_CMD.DO_MOUNT_CONTROL).Select(i => i.P1).ToList();
            CollectionAssert.AreEqual(new[] { -90.0, -45.0 }, mounts);
        }

        [TestMethod]
        public void FollowingTheGroundFromElevationData()
        {
            // a slope: 1 m up for every 10 m north
            Func<double, double, double?> Ground = (lat, lng) => 100 + (lat - Lat) * 111194.9 / 10;
            var lines = TerrainSurvey.Lines(Square, 0, 50);
            var o = new TerrainSurveyOptions { Height = 60, Mode = TerrainHeightMode.FromElevation, FollowStep = 50 };
            var items = TerrainSurvey.Build(lines, o, Ground);
            var waypoints = items.Where(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT).ToList();
            // 200 m lines in 50 m steps: 5 waypoints each, above sea level
            Assert.AreEqual(20, waypoints.Count);
            Assert.IsTrue(waypoints.All(w => w.Frame == MAVLink.MAV_FRAME.GLOBAL));
            foreach (var w in waypoints)
                Assert.AreEqual(Ground(w.Lat, w.Lng).Value + 60, w.Alt, 1e-6);
            // 20 m of climb over the 200 m
            Assert.AreEqual(20, waypoints.Max(w => w.Alt) - waypoints.Min(w => w.Alt), 0.5);

            // without the data there is no mission
            Assert.ThrowsException<ArgumentException>(() => TerrainSurvey.Build(lines, o, (a, b) => null));
        }

        [TestMethod]
        public void FlightsByBattery()
        {
            var lines = TerrainSurvey.Lines(Square, 0, 20);
            Func<IList<SurveyLine>, List<InspectionItem>> build = l => TerrainSurvey.Build(l, new TerrainSurveyOptions());
            var whole = InspectionPattern.MissionSeconds(build(lines), null, 5, 2.5);
            Assert.AreEqual(1, TerrainSurvey.Flights(lines, build, null, 5, 2.5, whole + 1).Count);

            // each flight has its own take-off and landing: at least 3 flights for 2.5 times less time each
            var budget = whole / 2.5;
            var flights = TerrainSurvey.Flights(lines, build, null, 5, 2.5, budget);
            Assert.IsTrue(flights.Count >= 3, flights.Count.ToString());
            Assert.IsTrue(flights.All(f => f.Seconds <= budget));
            // every line once, in order, each flight ending with RTL
            Assert.AreEqual(1, flights[0].FirstLine);
            Assert.AreEqual(lines.Count, flights.Last().LastLine);
            for (var i = 1; i < flights.Count; i++)
                Assert.AreEqual(flights[i - 1].LastLine + 1, flights[i].FirstLine);
            Assert.AreEqual(lines.Count * 2, flights.Sum(f => f.Items.Count(i => i.Cmd == MAVLink.MAV_CMD.WAYPOINT)));
            Assert.IsTrue(flights.All(f => f.Items.Last().Cmd == MAVLink.MAV_CMD.RETURN_TO_LAUNCH));
        }

        [TestMethod]
        public void GroundRangeFromSamples()
        {
            Func<double, double, double?> Ground = (lat, lng) => 100 + (lat - Lat) * 111194.9 / 10;
            var range = TerrainSurvey.GroundRange(Square, 20, Ground);
            Assert.IsNotNull(range);
            Assert.AreEqual(100, range.Value.low, 2);
            Assert.AreEqual(120, range.Value.high, 2);
            Assert.IsNull(TerrainSurvey.GroundRange(Square, 20, (a, b) => null));
        }
    }
}
