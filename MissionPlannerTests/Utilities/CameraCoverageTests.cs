using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MissionPlanner.Utilities.Tests
{
    [TestClass]
    public class CameraCoverageTests
    {
        /// <summary>A 1-inch, 20 MP camera with an 8.8 mm lens: at 10 m a photo covers 15 × 10 m.</summary>
        static readonly CameraSpec OneInch = new CameraSpec
        {
            Name = "1-inch 20MP", FocalLength = 8.8, SensorWidth = 13.2, SensorHeight = 8.8, ImageWidth = 5472,
            ImageHeight = 3648
        };

        [TestMethod]
        public void FootprintAndPixelSize()
        {
            var (width, height) = CameraCoverage.Footprint(OneInch, 10);
            Assert.AreEqual(15, width, 1e-9);
            Assert.AreEqual(10, height, 1e-9);
            Assert.AreEqual(2.741, CameraCoverage.Gsd(OneInch, 10), 0.001);
            // and back: the distance for 2 mm a pixel
            Assert.AreEqual(7.296, CameraCoverage.DistanceFor(OneInch, 2), 0.001);
            Assert.AreEqual(2, CameraCoverage.Gsd(OneInch, CameraCoverage.DistanceFor(OneInch, 2)), 1e-9);
        }

        [TestMethod]
        public void SpacingForTheOverlap()
        {
            Assert.AreEqual(3, CameraCoverage.PhotoSpacing(OneInch, 10, 0.8), 1e-9);
            Assert.AreEqual(3, CameraCoverage.LevelSpacing(OneInch, 10, 0.7), 1e-9);
            Assert.AreEqual(7.5, CameraCoverage.PhotoSpacing(OneInch, 10, 0.5), 1e-9);
        }

        [TestMethod]
        public void PhotosPerOrbit()
        {
            // a 20 m wide building from 20 m out: 10 m to its face, 3 m apart on its 62.8 m round, but never fewer
            // than one every 15 degrees
            Assert.AreEqual(24, CameraCoverage.OrbitPhotos(OneInch, 20, 10, 0.8));
            // a 60 m wide tank from 40 m out: 62.8 photos' worth round its 188 m outside
            Assert.AreEqual(63, CameraCoverage.OrbitPhotos(OneInch, 40, 30, 0.8));
            // a pole: views from all round
            Assert.AreEqual(24, CameraCoverage.OrbitPhotos(OneInch, 15, 0, 0.8));
            // more overlap, more photos
            Assert.IsTrue(CameraCoverage.OrbitPhotos(OneInch, 40, 30, 0.9) > 63);
        }

        [TestMethod]
        public void CameraFilesInAnyCulture()
        {
            const string xml = @"<?xml version=""1.0"" encoding=""us-ascii""?>
<Cameras>
  <Camera><name>Good</name><flen>8.8</flen><imgh>3648</imgh><imgw>5472</imgw><senh>8.8</senh><senw>13.2</senw></Camera>
  <Camera><name>No lens</name><flen></flen><imgh>3648</imgh><imgw>5472</imgw><senh>8.8</senh><senw>13.2</senw></Camera>
</Cameras>";
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                var cameras = CameraCoverage.Read(xml);
                Assert.AreEqual(1, cameras.Count);
                Assert.AreEqual(8.8, cameras[0].FocalLength, 1e-9);
                Assert.AreEqual(13.2, cameras[0].SensorWidth, 1e-9);
                Assert.AreEqual(5472, cameras[0].ImageWidth);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [TestMethod]
        public void BuiltInCamerasRead()
        {
            // camerasBuiltin.xml at the top of the repository, found from wherever the tests run
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "camerasBuiltin.xml")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "camerasBuiltin.xml");

            var cameras = CameraCoverage.Read(File.ReadAllText(Path.Combine(dir.FullName, "camerasBuiltin.xml")));
            Assert.IsTrue(cameras.Count >= 25, cameras.Count.ToString());
            var a6000 = cameras.First(c => c.Name == "Sony A6000");
            Assert.IsTrue(a6000.ImageWidth > a6000.ImageHeight);
            Assert.IsTrue(CameraCoverage.Gsd(a6000, 10) > 0.5 && CameraCoverage.Gsd(a6000, 10) < 5);

            // bigW's Sony A6400 with a 16 mm lens: 14.7 m across from 10 m, 2.45 mm a pixel; 2 mm a pixel at 8.2 m
            var a6400 = cameras.First(c => c.Name == "Sony A6400 16mm (A6100, A6600)");
            Assert.AreEqual(14.69, CameraCoverage.Footprint(a6400, 10).width, 0.01);
            Assert.AreEqual(2.45, CameraCoverage.Gsd(a6400, 10), 0.01);
            Assert.AreEqual(8.17, CameraCoverage.DistanceFor(a6400, 2), 0.01);
            foreach (var name in new[] { "Sony A6400 20mm (A6100, A6600)", "Sony A6400 35mm (A6100, A6600)",
                         "Sony A7R IV 35mm", "DJI Zenmuse P1 35mm", "DJI Mavic 3 Enterprise wide", "DJI Phantom 4 Pro / RTK" })
                Assert.IsTrue(cameras.Any(c => c.Name == name), name);
        }
    }
}
