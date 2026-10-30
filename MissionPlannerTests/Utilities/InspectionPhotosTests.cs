using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using ExifLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MissionPlanner.Utilities.Tests
{
    [TestClass]
    public class InspectionPhotosTests
    {
        static readonly DateTime Start = new DateTime(2026, 10, 29, 10, 0, 0);
        const double Lat = 21.0285, Lng = 105.8542;

        /// <summary>
        /// An orbit of radius 30 m: 8 photos at 5 m and 8 at 12 m, every 3 s, then one over the middle at 20 m.
        /// </summary>
        static List<PhotoPosition> Orbit()
        {
            var list = new List<PhotoPosition>();
            var n = 0;
            foreach (var height in new[] { 5.0, 12.0 })
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Math.PI / 4;
                    list.Add(new PhotoPosition
                    {
                        Index = ++n, Time = Start.AddSeconds(3 * n),
                        Lat = Lat + 30 * Math.Cos(angle) / 111195, Lng = Lng + 30 * Math.Sin(angle) / (111195 * Math.Cos(Lat * Math.PI / 180)),
                        AltMsl = 10 + height, Height = height, Yaw = (i * 45 + 180) % 360
                    });
                }

            list.Add(new PhotoPosition { Index = ++n, Time = Start.AddSeconds(3 * n), Lat = Lat, Lng = Lng, AltMsl = 30, Height = 20, Pitch = -90 });
            return list;
        }

        static IEnumerable<(string, DateTime)> Files(IList<PhotoPosition> positions, TimeSpan clock)
        {
            return positions.Select(p => ("DSC" + (1000 + p.Index) + ".JPG", p.Time + clock));
        }

        [TestMethod]
        public void PositionsReadBackInAnyCulture()
        {
            var culture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("vi-VN");
                var orbit = Orbit();
                var back = InspectionPhotos.ReadPositions(InspectionPhotos.WritePositions(orbit));
                Assert.AreEqual(orbit.Count, back.Count);
                Assert.AreEqual(orbit[3].Lat, back[3].Lat, 1e-7);
                Assert.AreEqual(orbit[3].Time, back[3].Time);
                Assert.AreEqual(orbit[3].Height, back[3].Height, 0.01);
                Assert.AreEqual(orbit[3].Yaw, back[3].Yaw, 0.1);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [TestMethod]
        public void MatchedInOrderWhenTheCountsAgree()
        {
            var orbit = Orbit();
            // the camera's clock is 3 hours and a bit off, and the files come unsorted
            var files = Files(orbit, TimeSpan.FromHours(3.01)).Reverse();
            var photos = InspectionPhotos.Match(files, orbit, out var byOrder);
            Assert.IsTrue(byOrder);
            Assert.AreEqual("DSC1001.JPG", photos[0].Source);
            Assert.AreSame(orbit[0], photos[0].Position);
            Assert.AreSame(orbit[16], photos[16].Position);
        }

        [TestMethod]
        public void MatchedByTimeWhenACountDiffers()
        {
            var orbit = Orbit();
            var files = Files(orbit, TimeSpan.FromMinutes(-7)).ToList();
            // the camera missed photos 5 and 6, and took one more 40 s after the last
            files.RemoveRange(4, 2);
            files.Add(("DSC9999.JPG", files.Last().Item2.AddSeconds(40)));
            var photos = InspectionPhotos.Match(files, orbit, out var byOrder);
            Assert.IsFalse(byOrder);
            Assert.AreEqual(16, photos.Count);
            Assert.AreSame(orbit[6], photos.First(p => p.Source == "DSC1007.JPG").Position);
            Assert.IsNull(photos.First(p => p.Source == "DSC9999.JPG").Position);
            Assert.AreEqual(15, photos.Count(p => p.Position != null));
        }

        [TestMethod]
        public void FloorsSidesAndNames()
        {
            Assert.AreEqual(1, InspectionPhotos.Floor(1, 0, 3.3));
            Assert.AreEqual(1, InspectionPhotos.Floor(3.2, 0, 3.3));
            Assert.AreEqual(2, InspectionPhotos.Floor(3.4, 0, 3.3));
            Assert.AreEqual(4, InspectionPhotos.Floor(10, 0, 3.3));
            // below the first floor is still floor 1
            Assert.AreEqual(1, InspectionPhotos.Floor(1, 2, 3.3));

            var orbit = Orbit();
            var photos = InspectionPhotos.Match(Files(orbit, TimeSpan.Zero), orbit, out _);
            InspectionPhotos.Name(photos, new InspectionPhotoOptions { Prefix = "Tòa A/1", FloorHeight = 3.3 });
            Assert.AreEqual("Tòa_A_1_F02_N_0001.JPG", photos[0].Name);
            Assert.AreEqual("E", photos[2].Side);
            Assert.AreEqual("SW", photos[5].Side);
            Assert.AreEqual(4, photos[8].Floor);
            Assert.AreEqual("Tòa_A_1_F04_N_0009.JPG", photos[8].Name);
            Assert.AreEqual("TOP", photos[16].Side);
            Assert.AreEqual(7, photos[16].Floor);
            Assert.AreEqual(17, photos.Select(p => p.Name).Distinct().Count());
            Assert.AreEqual("Photo", InspectionPhotos.Safe(" / "));
        }

        [TestMethod]
        public void ListsForPhotogrammetryAndMaps()
        {
            var orbit = Orbit();
            var photos = InspectionPhotos.Match(Files(orbit, TimeSpan.Zero), orbit, out _);
            InspectionPhotos.Name(photos, new InspectionPhotoOptions { Prefix = "T" });

            var geo = InspectionPhotos.GeoTxt(photos).Split('\n');
            Assert.AreEqual("EPSG:4326", geo[0]);
            StringAssert.Contains(geo[17], "T_F07_TOP_0017.JPG 105.8542000 21.0285000 30.00");

            var csv = InspectionPhotos.Csv(photos).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(18, csv.Length);
            Assert.IsTrue(csv[1].StartsWith("T_F02_N_0001.JPG,DSC1001.JPG,2026-10-29 10:00:03,21.0287"), csv[1]);

            var kml = XDocument.Parse(InspectionPhotos.Kml(photos, "T"));
            XNamespace k = "http://www.opengis.net/kml/2.2";
            Assert.AreEqual(17, kml.Descendants(k + "Placemark").Count());
            Assert.AreEqual(3, kml.Descendants(k + "Folder").Count());
        }

        static double D(MathEx.UFraction32 f) => (double)f.Numerator / f.Denominator;

        /// <summary>
        /// An 8 x 8 JPEG. As bytes: the test project sees two System.Drawing namespaces (the framework's and
        /// MissionPlanner.Drawing), so Bitmap is ambiguous there.
        /// </summary>
        const string TinyJpeg =
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAx" +
            "NDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy" +
            "MjIyMjIyMjL/wAARCAAIAAgDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUF" +
            "BAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVW" +
            "V1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi" +
            "4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAEC" +
            "AxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVm" +
            "Z2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq" +
            "8vP09fb3+Pn6/9oADAMBAAIRAxEAPwCrRRRXvngn/9k=";

        /// <summary>A small JPEG with the time the camera took it.</summary>
        static void Jpeg(string path, DateTime taken)
        {
            File.WriteAllBytes(path, Convert.FromBase64String(TinyJpeg));
            var image = ImageFile.FromFile(path);
            image.Properties.Add(ExifTag.DateTimeOriginal, taken);
            image.Save(path);
        }

        [TestMethod]
        public void FolderNamedAndGeotagged()
        {
            var root = Path.Combine(Path.GetTempPath(), "photos-test-" + Guid.NewGuid().ToString("N"));
            var source = Path.Combine(root, "card");
            Directory.CreateDirectory(source);
            try
            {
                var orbit = Orbit().Take(3).ToList();
                for (var i = 0; i < 3; i++)
                    Jpeg(Path.Combine(source, "DSC" + (1001 + i) + ".JPG"), orbit[i].Time.AddMinutes(5));
                File.WriteAllText(Path.Combine(source, "notes.txt"), "not a photo");

                Assert.AreEqual(orbit[1].Time.AddMinutes(5), InspectionPhotoFiles.Taken(Path.Combine(source, "DSC1002.JPG")));

                var output = Path.Combine(root, "out");
                var result = InspectionPhotoFiles.Process(source, orbit,
                    new InspectionPhotoOptions { Prefix = "Pole", FloorHeight = 3 }, output, true);
                Assert.AreEqual(3, result.Placed);
                Assert.IsTrue(result.ByOrder);
                foreach (var name in new[] { "photos.csv", "geo.txt", "photos.kml" })
                    Assert.IsTrue(File.Exists(Path.Combine(output, name)), name);

                var first = Path.Combine(output, result.Photos[0].Name);
                Assert.IsTrue(File.Exists(first), first);
                var gps = ImageFile.FromFile(first).Properties.Get<GPSLatitudeLongitude>(ExifTag.GPSLatitude);
                Assert.IsNotNull(gps);
                // to a few centimetres, not the 3 m of a tenth of a second
                Assert.AreEqual(orbit[0].Lat, D(gps.Degrees) + D(gps.Minutes) / 60 + D(gps.Seconds) / 3600, 1e-6);
                var lng = ImageFile.FromFile(first).Properties.Get<GPSLatitudeLongitude>(ExifTag.GPSLongitude);
                Assert.AreEqual(orbit[0].Lng, D(lng.Degrees) + D(lng.Minutes) / 60 + D(lng.Seconds) / 3600, 1e-6);
                var altitude = ImageFile.FromFile(first).Properties.Get<ExifURational>(ExifTag.GPSAltitude);
                Assert.AreEqual(orbit[0].AltMsl, D(altitude.Value), 0.01);
                // the original is untouched
                Assert.IsNull(ImageFile.FromFile(Path.Combine(source, "DSC1001.JPG")).Properties
                    .Get<GPSLatitudeLongitude>(ExifTag.GPSLatitude));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
