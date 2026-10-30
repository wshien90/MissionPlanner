using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MissionPlanner.Utilities;

namespace MissionPlanner.Utilities.Tests
{
    [TestClass]
    public class InspectionPatternTests
    {
        static readonly PointLatLngAlt Center = new PointLatLngAlt(21.0285, 105.8542);

        static PointLatLngAlt Pos(InspectionItem item)
        {
            return new PointLatLngAlt(item.Lat, item.Lng);
        }

        [TestMethod]
        public void LevelsAreTopDownAndEvenlySpaced()
        {
            CollectionAssert.AreEqual(new[] { 30.0, 20.0, 10.0 }, InspectionPattern.Levels(10, 30, 10));
            CollectionAssert.AreEqual(new[] { 30.0, 20.0, 10.0 }, InspectionPattern.Levels(30, 10, 10));
            CollectionAssert.AreEqual(new[] { 20.0 }, InspectionPattern.Levels(20, 20, 5));

            var levels = InspectionPattern.Levels(10, 30, 8);
            Assert.AreEqual(4, levels.Count);
            Assert.AreEqual(30, levels[0], 1e-9);
            Assert.AreEqual(10, levels[3], 1e-9);
            Assert.AreEqual(levels[0] - levels[1], levels[2] - levels[3], 1e-9);
        }

        [TestMethod]
        public void OrbitCirclesTheCenterWithTheCameraOnIt()
        {
            var items = InspectionPattern.Orbit(Center, 20, 10, 30, 10, 8, 45, true, 1, true, true);

            // per level: ROI, 8 x (waypoint, photo); then ROI none, climb, RTL
            Assert.AreEqual(3 * (1 + 8 * 2) + 3, items.Count);
            Assert.AreEqual(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, items.Last().Cmd);

            var orbit = items.Take(items.Count - 3).ToList();
            double roiAlt = -1;
            for (var i = 0; i < orbit.Count; i++)
            {
                var item = orbit[i];
                if (item.Cmd == MAVLink.MAV_CMD.DO_SET_ROI)
                {
                    Assert.AreEqual(Center.Lat, item.Lat, 1e-9);
                    Assert.AreEqual(Center.Lng, item.Lng, 1e-9);
                    roiAlt = item.Alt;
                }
                else if (item.Cmd == MAVLink.MAV_CMD.WAYPOINT)
                {
                    Assert.AreEqual(20, Center.GetDistance(Pos(item)), 0.2);
                    Assert.AreEqual(roiAlt, item.Alt, 1e-9, "the camera looks level at the structure");
                    Assert.AreEqual(1, item.P1, "hover");
                    Assert.AreEqual(MAVLink.MAV_CMD.DO_DIGICAM_CONTROL, orbit[i + 1].Cmd, "a photo after each point");
                }
            }

            var waypoints = items.Where(x => x.Cmd == MAVLink.MAV_CMD.WAYPOINT).ToList();
            Assert.AreEqual(45, Center.GetBearing(Pos(waypoints[0])), 0.5);
            Assert.AreEqual(90, Center.GetBearing(Pos(waypoints[1])), 0.5, "clockwise, 360 / 8 apart");
            Assert.AreEqual(30, waypoints[0].Alt);
            Assert.AreEqual(10, waypoints[3 * 8 - 1].Alt);

            var end = items[items.Count - 2];
            Assert.AreEqual(30, end.Alt, "climbs back to the top before leaving");
            Assert.AreEqual(0, items[items.Count - 3].Lat, "ROI cancelled");
        }

        [TestMethod]
        public void OrbitCounterClockwiseWithoutPhotos()
        {
            var items = InspectionPattern.Orbit(Center, 15, 20, 20, 5, 6, 0, false, 0, false, false);
            Assert.IsFalse(items.Any(x => x.Cmd == MAVLink.MAV_CMD.DO_DIGICAM_CONTROL));
            Assert.IsFalse(items.Any(x => x.Cmd == MAVLink.MAV_CMD.RETURN_TO_LAUNCH));

            var waypoints = items.Where(x => x.Cmd == MAVLink.MAV_CMD.WAYPOINT).ToList();
            Assert.AreEqual(300, Center.GetBearing(Pos(waypoints[1])), 0.5);
        }

        [TestMethod]
        public void FacadeFliesBesideTheWallFacingIt()
        {
            var start = Center;
            var end = Center.newpos(90, 40); // wall running east, 40 m
            var items = InspectionPattern.Facade(start, end, true, 10, 10, 30, 10, 10, 1, true, true);

            // 40 m at 10 m spacing: 5 points per pass, 3 passes of ROI + waypoint + photo; then ROI none, climb, RTL
            Assert.AreEqual(3 * 5 * 3 + 3, items.Count);

            var passes = items.Take(items.Count - 3).ToList();
            for (var i = 0; i < passes.Count; i += 3)
            {
                var roi = passes[i];
                var wp = passes[i + 1];
                Assert.AreEqual(MAVLink.MAV_CMD.DO_SET_ROI, roi.Cmd);
                Assert.AreEqual(MAVLink.MAV_CMD.WAYPOINT, wp.Cmd);
                Assert.AreEqual(MAVLink.MAV_CMD.DO_DIGICAM_CONTROL, passes[i + 2].Cmd);

                // the ROI is on the wall, the waypoint standoff metres straight out from it on the left (north)
                Assert.AreEqual(40, start.GetDistance(Pos(roi)) + Pos(roi).GetDistance(end), 0.2);
                Assert.AreEqual(10, Pos(roi).GetDistance(Pos(wp)), 0.2);
                Assert.AreEqual(0, Math.IEEERemainder(Pos(roi).GetBearing(Pos(wp)), 360), 0.5, "straight out from the wall");
                Assert.IsTrue(InspectionPattern.IsLeftOf(start, end, Pos(wp)));
                Assert.AreEqual(roi.Alt, wp.Alt, 1e-9);
            }

            var waypoints = items.Where(x => x.Cmd == MAVLink.MAV_CMD.WAYPOINT).ToList();
            Assert.AreEqual(0, start.GetDistance(Pos(waypoints[0])) - 10, 0.2, "first pass starts at the wall start");
            Assert.AreEqual(10, end.GetDistance(Pos(waypoints[5])), 0.2, "second pass starts at the wall end");
            Assert.AreEqual(30, waypoints[0].Alt);
            Assert.AreEqual(10, waypoints[14].Alt);
            Assert.AreEqual(30, waypoints.Last().Alt, "climbs back to the top before leaving");
        }

        [TestMethod]
        public void FacadeRightSide()
        {
            var end = Center.newpos(90, 20);
            var items = InspectionPattern.Facade(Center, end, false, 8, 15, 15, 5, 5, 0, false, false);
            foreach (var wp in items.Where(x => x.Cmd == MAVLink.MAV_CMD.WAYPOINT))
                Assert.IsFalse(InspectionPattern.IsLeftOf(Center, end, Pos(wp)));
        }

        [TestMethod]
        public void PathLengthIncludesClimbs()
        {
            var items = InspectionPattern.Orbit(Center, 20, 10, 30, 20, 4, 0, true, 0, false, false);
            // 2 levels of 4 points: 3 sides at each level, a diagonal side down to the second level, and a
            // diagonal side up from its last point to the first point at the top
            var side = Center.newpos(0, 20).GetDistance(Center.newpos(90, 20));
            var expected = 6 * side + 2 * Math.Sqrt(side * side + 20 * 20);
            Assert.AreEqual(expected, InspectionPattern.PathLength(items), 1);
        }

        [TestMethod]
        public void FlightTimeAccountsForAccelerationHoverAndStops()
        {
            var a = new InspectionItem { Cmd = MAVLink.MAV_CMD.WAYPOINT, Lat = Center.Lat, Lng = Center.Lng, Alt = 20 };
            var far = Center.newpos(0, 100);
            var b = new InspectionItem { Cmd = MAVLink.MAV_CMD.WAYPOINT, Lat = far.Lat, Lng = far.Lng, Alt = 20, P1 = 2 };
            var near = Center.newpos(0, 2.5);
            var c = new InspectionItem { Cmd = MAVLink.MAV_CMD.WAYPOINT, Lat = near.Lat, Lng = near.Lng, Alt = 20 };

            // 100 m at 10 m/s, 2.5 m/s/s: reaches speed, 100 / 10 + 10 / 2.5 = 14 s; then hover 2 s
            Assert.AreEqual(2 * InspectionPattern.StopOverhead + 14 + 2, InspectionPattern.FlightTime(new[] { a, b }, 10, 2.5), 0.1);
            // 2.5 m: never reaches speed, 2 * sqrt(2.5 / 2.5) = 2 s
            Assert.AreEqual(2 * InspectionPattern.StopOverhead + 2, InspectionPattern.FlightTime(new[] { a, c }, 10, 2.5), 0.1);
        }

        [TestMethod]
        public void OrbitLegsPassInsideTheRadius()
        {
            // 3 points: each straight leg passes radius * cos(60) from the center
            Assert.AreEqual(4, InspectionPattern.ClosestApproach(8, 3), 1e-9);
            Assert.AreEqual(20 * Math.Cos(Math.PI / 12), InspectionPattern.ClosestApproach(20, 12), 1e-9);
        }

        [TestMethod]
        public void SideOfGivesSignedMetres()
        {
            var end = Center.newpos(90, 100); // east
            Assert.AreEqual(30, InspectionPattern.SideOf(Center, end, Center.newpos(0, 30)), 0.3, "north is on the left");
            Assert.AreEqual(-30, InspectionPattern.SideOf(Center, end, Center.newpos(180, 30)), 0.3);
            Assert.AreEqual(0, InspectionPattern.SideOf(Center, end, Center.newpos(90, 50)), 0.3);
        }

        [TestMethod]
        public void OnlyPositionsTheVehicleFliesToCount()
        {
            Assert.IsTrue(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.WAYPOINT));
            Assert.IsTrue(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.SPLINE_WAYPOINT));
            Assert.IsTrue(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.LOITER_TIME));
            Assert.IsFalse(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.ROI));
            Assert.IsFalse(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.DO_SET_ROI));
            Assert.IsFalse(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.DO_ORBIT));
            Assert.IsFalse(InspectionPattern.IsFlyTo((ushort)MAVLink.MAV_CMD.RETURN_TO_LAUNCH));
        }

        [TestMethod]
        public void RefusesPatternsTooBigToStore()
        {
            // 10 to 1000 m every metre at 72 photos: 71,000 waypoints
            var ex = Assert.ThrowsException<ArgumentException>(() =>
                InspectionPattern.Orbit(Center, 20, 10, 1000, 1, 72, 0, true, 0, true, true));
            Assert.IsTrue(ex.Message.Contains("1000"));
            Assert.ThrowsException<ArgumentException>(() =>
                InspectionPattern.Facade(Center, Center.newpos(90, 2000), true, 10, 10, 200, 1, 1, 0, true, true));
        }

        [TestMethod]
        public void RejectsBadInput()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                InspectionPattern.Orbit(Center, 0, 10, 20, 5, 8, 0, true, 0, true, true));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                InspectionPattern.Orbit(Center, 10, 10, 20, 5, 2, 0, true, 0, true, true));
            Assert.ThrowsException<ArgumentException>(() =>
                InspectionPattern.Facade(Center, Center, true, 10, 10, 20, 5, 5, 0, true, true));
        }

        [TestMethod]
        public void SplitLevelsFitTheBudget()
        {
            var levels = new List<double> { 30, 25, 20, 15, 10 };
            // 100 s for take-off and the way there and back, 50 s a level: two levels make 200 s, three 250 s
            Func<IList<double>, double> seconds = l => 100 + 50 * l.Count;
            var flights = InspectionPattern.SplitLevels(levels, seconds, 220);
            Assert.AreEqual(3, flights.Count);
            CollectionAssert.AreEqual(new List<double> { 30, 25 }, flights[0]);
            CollectionAssert.AreEqual(new List<double> { 20, 15 }, flights[1]);
            CollectionAssert.AreEqual(new List<double> { 10 }, flights[2]);

            // all in one when it fits, and a level too long on its own is still flown
            Assert.AreEqual(1, InspectionPattern.SplitLevels(levels, seconds, 1000).Count);
            Assert.AreEqual(5, InspectionPattern.SplitLevels(levels, seconds, 10).Count);
        }

        [TestMethod]
        public void MissionTimeCountsTheWayThereAndBack()
        {
            var items = InspectionPattern.Orbit(Center, 15, 20, 20, 5, 12, 0, true, 1, true, true);
            var alone = InspectionPattern.MissionSeconds(items, null, 5, 2.5);
            Assert.AreEqual(InspectionPattern.FlightTime(items, 5, 2.5) + InspectionPattern.TakeoffLandingSeconds, alone, 1e-6);

            // home 100 m south of the centre: 115 m to the first point due north and 115 m back from the last
            var home = Center.newpos(180, 100);
            var withHome = InspectionPattern.MissionSeconds(items, home, 5, 2.5);
            Assert.AreEqual(alone + 230 / 5.0, withHome, 1);
        }

        [TestMethod]
        public void InspectionAsFlights()
        {
            var levels = InspectionPattern.Levels(10, 30, 2.5);
            Func<IList<double>, List<InspectionItem>> build = l =>
                InspectionPattern.Orbit(Center, 15, l, 24, 0, true, 1, true, true);
            var whole = InspectionPattern.MissionSeconds(build(levels), null, 5, 2.5);

            Assert.AreEqual(1, InspectionPattern.Flights(levels, build, null, 5, 2.5, whole + 1).Count);

            var budget = whole / 2.5;
            var flights = InspectionPattern.Flights(levels, build, null, 5, 2.5, budget);
            Assert.AreEqual(3, flights.Count);
            Assert.IsTrue(flights.All(f => f.Seconds <= budget), string.Join(", ", flights.Select(f => f.Seconds)));
            // every level once, top down, each flight its own orbits ending with RTL
            CollectionAssert.AreEqual(levels, flights.SelectMany(f => f.Items.Where(i => i.Cmd == MAVLink.MAV_CMD.DO_SET_ROI &&
                i.HasPosition).Select(i => i.Alt)).ToList());
            Assert.AreEqual(30, flights[0].TopAlt, 1e-9);
            Assert.AreEqual(10, flights.Last().BottomAlt, 1e-9);
            Assert.IsTrue(flights.All(f => f.Items.Last().Cmd == MAVLink.MAV_CMD.RETURN_TO_LAUNCH));
            Assert.AreEqual(9 * 24, flights.Sum(f => f.Items.Count(i => i.Cmd == MAVLink.MAV_CMD.DO_DIGICAM_CONTROL)));
        }

        [TestMethod]
        public void FlightsSavedAsMissionFiles()
        {
            var levels = InspectionPattern.Levels(10, 30, 2.5);
            Func<IList<double>, List<InspectionItem>> build = l =>
                InspectionPattern.Orbit(Center, 15, l, 24, 0, true, 1, true, true);
            var flights = InspectionPattern.Flights(levels, build, null, 5, 2.5,
                InspectionPattern.MissionSeconds(build(levels), null, 5, 2.5) / 2.5);
            var home = new PointLatLngAlt(21.0280, 105.8530, 12.5);
            // a take-off the mission had before the inspection
            var before = new List<Locationwp>
                { new Locationwp { id = (ushort)MAVLink.MAV_CMD.TAKEOFF, frame = 3, alt = 30 } };

            Assert.AreEqual("inspection_20261030_091502", InspectionFlightFiles.FolderName(new DateTime(2026, 10, 30, 9, 15, 2)));
            var folder = Path.Combine(Path.GetTempPath(), "flights-test-" + Guid.NewGuid().ToString("N"));
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                // a decimal comma must not get into the files
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                var files = InspectionFlightFiles.Save(folder, home, before, flights, 3);
                Assert.AreEqual(3, files.Count);
                Assert.AreEqual("flight_2_of_3.waypoints", Path.GetFileName(files[1]));
                Thread.CurrentThread.CurrentCulture = culture;

                for (var k = 0; k < files.Count; k++)
                {
                    // read back as the planner opens a mission file: home, the take-off, then the flight
                    var read = WaypointFile.ReadWaypointFile(files[k]);
                    Assert.AreEqual(2 + flights[k].Items.Count, read.Count);
                    Assert.AreEqual(home.Lat, read[0].lat, 1e-7);
                    Assert.AreEqual(12.5, read[0].alt, 1e-6);
                    Assert.AreEqual((ushort)MAVLink.MAV_CMD.TAKEOFF, read[1].id);
                    Assert.AreEqual(30, read[1].alt, 1e-6);
                    for (var i = 0; i < flights[k].Items.Count; i++)
                    {
                        var item = flights[k].Items[i];
                        var row = read[2 + i];
                        Assert.AreEqual((ushort)item.Cmd, row.id);
                        Assert.AreEqual((byte)3, row.frame);
                        Assert.AreEqual(item.P1, row.p1, 1e-6);
                        Assert.AreEqual(item.Lat, row.lat, 1e-7);
                        Assert.AreEqual(item.Lng, row.lng, 1e-7);
                        Assert.AreEqual(item.Alt, row.alt, 1e-4);
                    }
                }

                // without a home the home row is empty, as the planner writes it
                var text = InspectionFlightFiles.Waypoints(null, new List<Locationwp>(), flights[0].Items, 3);
                StringAssert.StartsWith(text, "QGC WPL 110\r\n0\t1\t0\t0\t");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }
    }
}
