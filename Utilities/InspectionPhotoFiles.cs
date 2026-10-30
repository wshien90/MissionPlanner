using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExifLibrary;
using log4net;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Copies a folder of inspection photos into an output folder, named by floor and side and with their GPS
    /// position written in, with photos.csv, geo.txt and photos.kml beside them. The originals are not changed.
    /// </summary>
    public static class InspectionPhotoFiles
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(InspectionPhotoFiles));

        static readonly string[] Extensions = { ".jpg", ".jpeg", ".tif", ".tiff", ".dng" };

        public class Result
        {
            public List<InspectionPhoto> Photos;
            /// <summary>True when photos and positions were paired in order, false by time.</summary>
            public bool ByOrder;
            public string Folder;
            public int Placed => Photos.Count(p => p.Position != null);
            public int Unplaced => Photos.Count(p => p.Position == null);
        }

        /// <summary>The photo files of a folder, not of its subfolders.</summary>
        public static List<string> Files(string folder)
        {
            return Directory.GetFiles(folder)
                .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
        }

        /// <summary>When the camera took the photo (EXIF DateTimeOriginal), else the file's time.</summary>
        public static DateTime Taken(string file)
        {
            try
            {
                var image = ImageFile.FromFile(file);
                var original = image.Properties.Get<ExifDateTime>(ExifTag.DateTimeOriginal);
                if (original != null)
                    return original.Value;
            }
            catch (Exception ex)
            {
                log.Info("photo time: " + file + ": " + ex.Message);
            }

            return File.GetLastWriteTime(file);
        }

        /// <summary>
        /// Does it all; progress gets the share done and the file. Photos without a position are copied into
        /// "unplaced" under their own name.
        /// </summary>
        public static Result Process(string photoFolder, IList<PhotoPosition> positions, InspectionPhotoOptions options,
            string outFolder, bool geotag, Action<int, string> progress = null, Func<bool> cancelled = null)
        {
            var files = Files(photoFolder);
            var photos = InspectionPhotos.Match(files.Select(f => (f, Taken(f))), positions, out var byOrder);
            InspectionPhotos.Name(photos, options);

            Directory.CreateDirectory(outFolder);
            for (var i = 0; i < photos.Count; i++)
            {
                if (cancelled?.Invoke() == true)
                    throw new OperationCanceledException();
                var photo = photos[i];
                progress?.Invoke(i * 100 / Math.Max(1, photos.Count), Path.GetFileName(photo.Source));

                if (photo.Position == null)
                {
                    var unplaced = Path.Combine(outFolder, "unplaced");
                    Directory.CreateDirectory(unplaced);
                    File.Copy(photo.Source, Path.Combine(unplaced, photo.Name), true);
                    continue;
                }

                var target = Path.Combine(outFolder, photo.Name);
                var jpeg = Path.GetExtension(photo.Source).ToLowerInvariant().StartsWith(".jp");
                if (!(geotag && jpeg && WriteGps(photo.Source, target, photo.Position)))
                    File.Copy(photo.Source, target, true);
            }

            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(outFolder, "photos.csv"), InspectionPhotos.Csv(photos), new UTF8Encoding(true));
            File.WriteAllText(Path.Combine(outFolder, "geo.txt"), InspectionPhotos.GeoTxt(photos), utf8);
            File.WriteAllText(Path.Combine(outFolder, "photos.kml"),
                InspectionPhotos.Kml(photos, InspectionPhotos.Safe(options.Prefix)), utf8);
            progress?.Invoke(100, "");
            return new Result { Photos = photos, ByOrder = byOrder, Folder = outFolder };
        }

        /// <summary>
        /// Degrees, minutes and seconds to 1/10000 of a second (3 mm): the float overload keeps a tenth of a second,
        /// 3 m, too coarse for photogrammetry.
        /// </summary>
        static GPSLatitudeLongitude Dms(ExifTag tag, double angle)
        {
            angle = Math.Abs(angle);
            var degrees = (uint)Math.Floor(angle);
            var minutes = (uint)Math.Floor((angle - degrees) * 60);
            var tenThousandths = (uint)Math.Round(((angle - degrees) * 60 - minutes) * 60 * 10000);
            return new GPSLatitudeLongitude(tag, new[]
            {
                new MathEx.UFraction32(degrees, 1), new MathEx.UFraction32(minutes, 1),
                new MathEx.UFraction32(tenThousandths, 10000)
            });
        }

        /// <summary>Copies a JPEG with its GPS position set; false when the file can't be read as one.</summary>
        public static bool WriteGps(string source, string target, PhotoPosition p)
        {
            try
            {
                var image = ImageFile.FromFile(source);
                foreach (var item in image.Properties.ToArray())
                    if (item.Tag == ExifTag.GPSLatitude || item.Tag == ExifTag.GPSLongitude ||
                        item.Tag == ExifTag.GPSLatitudeRef || item.Tag == ExifTag.GPSLongitudeRef ||
                        item.Tag == ExifTag.GPSAltitude || item.Tag == ExifTag.GPSAltitudeRef)
                        image.Properties.Remove(item);

                image.Properties.Add(Dms(ExifTag.GPSLatitude, p.Lat));
                image.Properties.Add(ExifTag.GPSLatitudeRef, p.Lat < 0 ? "S" : "N");
                image.Properties.Add(Dms(ExifTag.GPSLongitude, p.Lng));
                image.Properties.Add(ExifTag.GPSLongitudeRef, p.Lng < 0 ? "W" : "E");
                // to the centimetre; above sea level needs no reference
                image.Properties.Add(new ExifURational(ExifTag.GPSAltitude,
                    (uint)Math.Round(Math.Max(0, p.AltMsl) * 100), 100));
                image.Save(target);
                return true;
            }
            catch (Exception ex)
            {
                log.Warn("geotag: " + source + ": " + ex.Message);
                return false;
            }
        }
    }
}
