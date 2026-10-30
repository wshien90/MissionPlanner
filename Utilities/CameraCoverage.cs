using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>A camera as the Survey tool's camera files describe it: lens and sensor in mm, image in pixels.</summary>
    public class CameraSpec
    {
        public string Name;
        public double FocalLength, SensorWidth, SensorHeight;
        public int ImageWidth, ImageHeight;

        public override string ToString() => Name;
    }

    /// <summary>
    /// What a camera covers at a distance, and the photo and level spacing that give a wanted overlap, for
    /// photogrammetry of structures. The camera is held level (landscape), looking at the structure.
    /// </summary>
    public static class CameraCoverage
    {
        /// <summary>
        /// The largest turn between two orbit photos, degrees: a thin structure (a pole) needs views from all
        /// round to be rebuilt in 3D, whatever the overlap.
        /// </summary>
        public const double MaxOrbitStep = 15;

        /// <summary>Closest a lens may get to the surface for these sums, metres.</summary>
        public const double MinSurfaceDistance = 1;

        /// <summary>The cameras of camerasBuiltin.xml next to the program and the user's cameras.xml, by name.</summary>
        public static List<CameraSpec> Cameras()
        {
            var list = new List<CameraSpec>();
            foreach (var file in new[]
                     {
                         Path.Combine(Settings.GetRunningDirectory(), "camerasBuiltin.xml"),
                         Path.Combine(Settings.GetUserDataDirectory(), "cameras.xml")
                     })
            {
                if (!File.Exists(file))
                    continue;
                try
                {
                    list.AddRange(Read(File.ReadAllText(file)));
                }
                catch (Exception)
                {
                    // a broken camera file leaves the others
                }
            }

            // the user's own camera replaces a built-in one of the same name
            return list.GroupBy(c => c.Name).Select(g => g.Last()).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>The cameras in a camera file; those missing a value are skipped.</summary>
        public static List<CameraSpec> Read(string xml)
        {
            double D(XElement e, string name) =>
                double.TryParse(e.Element(name)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
            return XDocument.Parse(xml).Descendants("Camera").Select(e => new CameraSpec
            {
                Name = e.Element("name")?.Value?.Trim() ?? "",
                FocalLength = D(e, "flen"), SensorWidth = D(e, "senw"), SensorHeight = D(e, "senh"),
                ImageWidth = (int)D(e, "imgw"), ImageHeight = (int)D(e, "imgh")
            }).Where(c => c.Name != "" && c.FocalLength > 0 && c.SensorWidth > 0 && c.SensorHeight > 0 &&
                          c.ImageWidth > 0 && c.ImageHeight > 0).ToList();
        }

        /// <summary>Metres of surface one photo covers, across and up, from distance metres away.</summary>
        public static (double width, double height) Footprint(CameraSpec camera, double distance)
        {
            return (distance * camera.SensorWidth / camera.FocalLength, distance * camera.SensorHeight / camera.FocalLength);
        }

        /// <summary>Ground sample distance: millimetres of surface per pixel, from distance metres away.</summary>
        public static double Gsd(CameraSpec camera, double distance)
        {
            return Footprint(camera, distance).width / camera.ImageWidth * 1000;
        }

        /// <summary>The distance in metres that gives a ground sample distance in mm per pixel.</summary>
        public static double DistanceFor(CameraSpec camera, double gsd)
        {
            return gsd / 1000 * camera.ImageWidth * camera.FocalLength / camera.SensorWidth;
        }

        /// <summary>Metres between photos along a surface for the overlap (0 to 1) between neighbours.</summary>
        public static double PhotoSpacing(CameraSpec camera, double distance, double overlap)
        {
            return Footprint(camera, distance).width * (1 - overlap);
        }

        /// <summary>Metres between levels for the overlap (0 to 1) between a level's photos and the next.</summary>
        public static double LevelSpacing(CameraSpec camera, double distance, double overlap)
        {
            return Footprint(camera, distance).height * (1 - overlap);
        }

        /// <summary>
        /// Photos per orbit of radius metres around a structure about structureRadius metres across from its
        /// centre: enough for the overlap along its outside, and at least one every MaxOrbitStep degrees.
        /// </summary>
        public static int OrbitPhotos(CameraSpec camera, double radius, double structureRadius, double overlap)
        {
            var surface = Math.Max(radius - structureRadius, MinSurfaceDistance);
            var spacing = PhotoSpacing(camera, surface, overlap);
            var byOverlap = structureRadius > 0 ? (int)Math.Ceiling(2 * Math.PI * structureRadius / spacing - 1e-9) : 0;
            var byAngle = (int)Math.Ceiling(360 / MaxOrbitStep - 1e-9);
            return Math.Max(byOverlap, byAngle);
        }
    }
}
