using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using log4net;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Records where each photo of a flight was taken (the vehicle's CAMERA_FEEDBACK), from arming to disarming,
    /// and saves them in the photos folder when the vehicle disarms or the link closes, so the photo files can be
    /// named and geotagged after the flight (Inspection photos). A flight without photos saves nothing.
    /// </summary>
    public static class PhotoRecorder
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PhotoRecorder));

        public static string Folder => Path.Combine(Settings.GetUserDataDirectory(), "photos");

        /// <summary>A flight's positions file is flight_yyyy-MM-dd_HH-mm-ss with this after it.</summary>
        public const string PhotosSuffix = "_photos.csv";

        static readonly object sync = new object();
        static Timer timer;
        static bool flying;
        static DateTime start;
        static int lastPhotos;
        static readonly List<PhotoPosition> photos = new List<PhotoPosition>();

        /// <summary>Starts watching for flights; call once.</summary>
        public static void Start()
        {
            if (timer != null)
                return;
            timer = new Timer(_ => Tick(), null, 1000, 1000);
        }

        static void Tick()
        {
            if (!Monitor.TryEnter(sync))
                return;
            try
            {
                var port = MainV2.comPort;
                var mav = port?.MAV;
                var cs = mav?.cs;
                if (cs == null)
                    return;
                var open = port.BaseStream?.IsOpen == true && !port.logreadmode;

                if (!flying)
                {
                    if (open && cs.armed)
                    {
                        flying = true;
                        start = DateTime.Now;
                        photos.Clear();
                        lastPhotos = mav.camerapoints.Count;
                    }

                    return;
                }

                if (open)
                    Collect(mav);
                if (!open || !cs.armed)
                {
                    flying = false;
                    Save();
                }
            }
            catch (Exception ex)
            {
                log.Error("photo recorder", ex);
            }
            finally
            {
                Monitor.Exit(sync);
            }
        }

        /// <summary>The photos taken since the last look.</summary>
        static void Collect(MAVState mav)
        {
            var points = mav.camerapoints.ToArray();
            if (points.Length < lastPhotos)
                lastPhotos = 0;
            foreach (var photo in points.Skip(lastPhotos))
            {
                photos.Add(new PhotoPosition
                {
                    Index = photo.img_idx,
                    // the vehicle's UTC time, else when it came in
                    Time = photo.time_usec > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds((long)(photo.time_usec / 1000)).LocalDateTime
                        : DateTime.Now,
                    Lat = photo.lat / 1e7, Lng = photo.lng / 1e7, AltMsl = photo.alt_msl, Height = photo.alt_rel,
                    Roll = photo.roll, Pitch = photo.pitch, Yaw = photo.yaw
                });
            }

            lastPhotos = points.Length;
        }

        static void Save()
        {
            if (photos.Count == 0)
                return;
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder,
                "flight_" + start.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + PhotosSuffix);
            File.WriteAllText(path, InspectionPhotos.WritePositions(photos), new UTF8Encoding(false));
            log.Info("photo recorder: " + photos.Count + " photos in " + path);
        }
    }
}
