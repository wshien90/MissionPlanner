using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MissionPlanner.Utilities
{
    /// <summary>Where the vehicle was when it took a photo (CAMERA_FEEDBACK), in metres and degrees.</summary>
    public class PhotoPosition
    {
        /// <summary>The camera's image number.</summary>
        public int Index;
        public DateTime Time;
        public double Lat, Lng;
        /// <summary>Above sea level, and above home.</summary>
        public double AltMsl, Height;
        /// <summary>The vehicle's attitude, not the gimbal's.</summary>
        public double Roll, Pitch, Yaw;
    }

    /// <summary>A photo file with the position it was taken at and the name it gets.</summary>
    public class InspectionPhoto
    {
        public string Source;
        public DateTime Taken;
        /// <summary>Null when no position matched.</summary>
        public PhotoPosition Position;
        public int Floor;
        /// <summary>N, NE, E, SE, S, SW, W, NW: the side of the structure the photo was taken from; TOP above it.</summary>
        public string Side = "";
        /// <summary>The new file name, extension included; the source's name when it has no position.</summary>
        public string Name;
    }

    /// <summary>What to do with a folder of inspection photos.</summary>
    public class InspectionPhotoOptions
    {
        /// <summary>The start of each name: the structure, e.g. "TowerA".</summary>
        public string Prefix = "Building";
        /// <summary>Metres per floor.</summary>
        public double FloorHeight = 3.3;
        /// <summary>The height above home of the first floor's floor, metres.</summary>
        public double GroundHeight;
        /// <summary>The structure's centre; the middle of the photo positions when null.</summary>
        public PointLatLngAlt Center;
    }

    /// <summary>
    /// Inspection photos: the positions recorded in flight, matched to the photo files, named by floor and side,
    /// and listed for photogrammetry (CSV, OpenDroneMap geo.txt) and maps (KML).
    /// </summary>
    public static class InspectionPhotos
    {
        /// <summary>Great-circle metres between two positions.</summary>
        public static double Metres(double lat1, double lng1, double lat2, double lng2)
        {
            const double radius = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLng = (lng2 - lng1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) *
                Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * radius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Photos this close to the middle, as a share of the median distance, are taken from above.</summary>
        public const double TopShare = 0.3;

        /// <summary>A photo and a position this far apart in time, once the clocks are aligned, don't match.</summary>
        public static readonly TimeSpan MatchWindow = TimeSpan.FromSeconds(2);

        const string PositionsHeader = "index,time,lat,lon,alt_msl,height,roll,pitch,yaw";

        /// <summary>The positions file the flight recorder writes next to a report.</summary>
        public static string WritePositions(IEnumerable<PhotoPosition> positions)
        {
            var text = new StringBuilder(PositionsHeader).Append("\r\n");
            foreach (var p in positions)
                text.Append(string.Format(Inv, "{0},{1:yyyy-MM-ddTHH:mm:ss.fff},{2:0.0000000},{3:0.0000000},{4:0.00},{5:0.00},{6:0.0},{7:0.0},{8:0.0}\r\n",
                    p.Index, p.Time, p.Lat, p.Lng, p.AltMsl, p.Height, p.Roll, p.Pitch, p.Yaw));
            return text.ToString();
        }

        public static List<PhotoPosition> ReadPositions(string csv)
        {
            var list = new List<PhotoPosition>();
            foreach (var line in csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                var f = line.Split(',');
                if (f.Length < 9)
                    continue;
                double D(int i) => double.Parse(f[i], Inv);
                list.Add(new PhotoPosition
                {
                    Index = int.Parse(f[0], Inv),
                    Time = DateTime.ParseExact(f[1], "yyyy-MM-ddTHH:mm:ss.fff", Inv),
                    Lat = D(2), Lng = D(3), AltMsl = D(4), Height = D(5), Roll = D(6), Pitch = D(7), Yaw = D(8)
                });
            }

            return list;
        }

        /// <summary>
        /// Pairs photos (file, time the camera took it) with positions: in order when there are as many of each,
        /// else by time once the camera's clock is aligned to the first position. Photos are sorted by time, then
        /// name. Unmatched photos have a null Position.
        /// </summary>
        public static List<InspectionPhoto> Match(IEnumerable<(string file, DateTime taken)> photos,
            IList<PhotoPosition> positions, out bool byOrder)
        {
            var sorted = photos.OrderBy(p => p.taken).ThenBy(p => p.file, StringComparer.OrdinalIgnoreCase)
                .Select(p => new InspectionPhoto { Source = p.file, Taken = p.taken }).ToList();
            var ordered = positions.OrderBy(p => p.Time).ToList();

            byOrder = sorted.Count == ordered.Count;
            if (byOrder)
            {
                for (var i = 0; i < sorted.Count; i++)
                    sorted[i].Position = ordered[i];
                return sorted;
            }

            if (sorted.Count == 0 || ordered.Count == 0)
                return sorted;

            // the camera's clock is its own: align its first photo with the first position, then nearest in time
            var offset = sorted[0].Taken - ordered[0].Time;
            var used = new HashSet<PhotoPosition>();
            foreach (var photo in sorted)
            {
                var when = photo.Taken - offset;
                var best = ordered.Where(p => !used.Contains(p))
                    .OrderBy(p => Math.Abs((p.Time - when).TotalSeconds)).FirstOrDefault();
                if (best != null && (best.Time - when).Duration() <= MatchWindow)
                {
                    photo.Position = best;
                    used.Add(best);
                }
            }

            return sorted;
        }

        /// <summary>The floor a height is at: 1 from the ground floor's height up, never below 1.</summary>
        public static int Floor(double height, double groundHeight, double floorHeight)
        {
            return Math.Max(1, (int)Math.Floor((height - groundHeight) / Math.Max(floorHeight, 0.5)) + 1);
        }

        static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>The compass side of the centre a position is on.</summary>
        public static string SideOf(PointLatLngAlt center, double lat, double lng)
        {
            var east = (lng - center.Lng) * Math.Cos(center.Lat * Math.PI / 180);
            var north = lat - center.Lat;
            var bearing = (Math.Atan2(east, north) * 180 / Math.PI + 360) % 360;
            return Compass[(int)Math.Round(bearing / 45) % 8];
        }

        /// <summary>
        /// Floors, sides and names "Prefix_F03_NE_0012.jpg", numbered in flight order; photos without a position
        /// keep their name.
        /// </summary>
        public static void Name(IList<InspectionPhoto> photos, InspectionPhotoOptions options)
        {
            var placed = photos.Where(p => p.Position != null).ToList();
            var center = options.Center;
            if (center == null && placed.Count > 0)
                center = new PointLatLngAlt(placed.Average(p => p.Position.Lat), placed.Average(p => p.Position.Lng));

            // from above: much closer to the middle than the others
            var distances = placed.Select(p => Metres(center.Lat, center.Lng, p.Position.Lat, p.Position.Lng))
                .OrderBy(d => d).ToList();
            var median = distances.Count > 0 ? distances[distances.Count / 2] : 0;

            var prefix = Safe(options.Prefix);
            var n = 0;
            foreach (var photo in photos)
            {
                var ext = Path.GetExtension(photo.Source);
                if (photo.Position == null)
                {
                    photo.Name = Path.GetFileName(photo.Source);
                    continue;
                }

                n++;
                var p = photo.Position;
                photo.Floor = Floor(p.Height, options.GroundHeight, options.FloorHeight);
                var distance = Metres(center.Lat, center.Lng, p.Lat, p.Lng);
                photo.Side = median > 0 && distance < median * TopShare ? "TOP" : SideOf(center, p.Lat, p.Lng);
                photo.Name = string.Format(Inv, "{0}_F{1:00}_{2}_{3:0000}{4}", prefix, photo.Floor, photo.Side, n, ext);
            }
        }

        /// <summary>A name prefix that is a valid file name: letters, digits, - and _.</summary>
        public static string Safe(string prefix)
        {
            var safe = new string((prefix ?? "").Trim().Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
            return safe.Trim('_') == "" ? "Photo" : safe;
        }

        /// <summary>One line per photo, for spreadsheets and photogrammetry imports (Metashape, Pix4D, RealityScan).</summary>
        public static string Csv(IEnumerable<InspectionPhoto> photos)
        {
            var text = new StringBuilder("name,original,taken,lat,lon,alt_msl,height,floor,side,yaw,pitch,roll\r\n");
            foreach (var p in photos.Where(x => x.Position != null))
                text.Append(string.Format(Inv, "{0},{1},{2:yyyy-MM-dd HH:mm:ss},{3:0.0000000},{4:0.0000000},{5:0.00},{6:0.00},{7},{8},{9:0.0},{10:0.0},{11:0.0}\r\n",
                    p.Name, CsvText(Path.GetFileName(p.Source)), p.Taken, p.Position.Lat, p.Position.Lng, p.Position.AltMsl,
                    p.Position.Height, p.Floor, p.Side, p.Position.Yaw, p.Position.Pitch, p.Position.Roll));
            return text.ToString();
        }

        static string CsvText(string text) => text.Contains(",") || text.Contains("\"") ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

        /// <summary>
        /// OpenDroneMap's geo.txt: positions only, as the vehicle's attitude is not the camera's.
        /// </summary>
        public static string GeoTxt(IEnumerable<InspectionPhoto> photos)
        {
            var text = new StringBuilder("EPSG:4326\n");
            foreach (var p in photos.Where(x => x.Position != null))
                text.Append(string.Format(Inv, "{0} {1:0.0000000} {2:0.0000000} {3:0.00}\n", p.Name, p.Position.Lng,
                    p.Position.Lat, p.Position.AltMsl));
            return text.ToString();
        }

        /// <summary>A placemark per photo, in a folder per floor, at its altitude, for Google Earth.</summary>
        public static string Kml(IEnumerable<InspectionPhoto> photos, string title)
        {
            XNamespace k = "http://www.opengis.net/kml/2.2";
            var document = new XElement(k + "Document", new XElement(k + "name", title));
            foreach (var floor in photos.Where(x => x.Position != null).GroupBy(x => x.Floor).OrderBy(g => g.Key))
            {
                var folder = new XElement(k + "Folder", new XElement(k + "name", FsemiText.Format("Photo_FloorName", floor.Key)));
                foreach (var p in floor)
                    folder.Add(new XElement(k + "Placemark",
                        new XElement(k + "name", Path.GetFileNameWithoutExtension(p.Name)),
                        new XElement(k + "description", string.Format(Inv, "{0} · {1:yyyy-MM-dd HH:mm:ss} · {2:0.0} m · {3}",
                            Path.GetFileName(p.Source), p.Taken, p.Position.Height, p.Side)),
                        new XElement(k + "Point",
                            new XElement(k + "altitudeMode", "absolute"),
                            new XElement(k + "coordinates", string.Format(Inv, "{0:0.0000000},{1:0.0000000},{2:0.00}",
                                p.Position.Lng, p.Position.Lat, p.Position.AltMsl)))));
                document.Add(folder);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(k + "kml", document)).ToString();
        }

        /// <summary>A count per floor and side, for the summary.</summary>
        public static string Summary(IEnumerable<InspectionPhoto> photos)
        {
            return string.Join("\n", photos.Where(p => p.Position != null).GroupBy(p => p.Floor).OrderBy(g => g.Key)
                .Select(g => FsemiText.Format("Photo_FloorName", g.Key) + ": " +
                             string.Join(", ", g.GroupBy(p => p.Side).OrderBy(s => Array.IndexOf(Compass, s.Key))
                                 .Select(s => s.Key + " " + s.Count()))));
        }
    }
}
