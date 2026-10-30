using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// The flights of an inspection split by battery as mission files, one .waypoints file a flight, so each can be
    /// opened again later or on another computer in the field.
    /// </summary>
    public static class InspectionFlightFiles
    {
        /// <summary>Where the flights of inspections are saved, a folder each.</summary>
        public static string Root => Path.Combine(Settings.GetUserDataDirectory(), "missions");

        /// <summary>A new folder name under Root for an inspection planned at that time.</summary>
        public static string FolderName(DateTime time)
        {
            return "inspection_" + time.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        }

        /// <summary>The file name of flight number (from 1) of count.</summary>
        public static string FileName(int number, int count)
        {
            return string.Format(CultureInfo.InvariantCulture, "flight_{0}_of_{1}.waypoints", number, count);
        }

        /// <summary>
        /// A mission file (QGC WPL 110, as the planner's Save writes it): home, the mission's rows before the
        /// inspection, then the flight, with the planner's altitude frame; altitudes in metres. Without a home
        /// the home row is all zeros, as the planner writes it.
        /// </summary>
        public static string Waypoints(PointLatLngAlt home, IList<Locationwp> before, IList<InspectionItem> flight,
            byte frame)
        {
            var en = CultureInfo.InvariantCulture;
            var text = new StringBuilder();
            text.Append("QGC WPL 110\r\n");
            if (home != null)
                text.Append("0\t1\t0\t16\t0\t0\t0\t0\t" + home.Lat.ToString("0.0000000", en) + "\t" +
                            home.Lng.ToString("0.0000000", en) + "\t" + home.Alt.ToString("0.000000", en) + "\t1\r\n");
            else
                text.Append("0\t1\t0\t0\t0\t0\t0\t0\t0\t0\t0\t1\r\n");

            var seq = 0;
            void Row(byte f, ushort cmd, double p1, double p2, double p3, double p4, double lat, double lng, double alt)
            {
                text.Append(++seq + "\t0\t" + f + "\t" + cmd + "\t" + p1.ToString("0.00000000", en) + "\t" +
                            p2.ToString("0.00000000", en) + "\t" + p3.ToString("0.00000000", en) + "\t" +
                            p4.ToString("0.00000000", en) + "\t" + lat.ToString("0.00000000", en) + "\t" +
                            lng.ToString("0.00000000", en) + "\t" + alt.ToString("0.000000", en) + "\t1\r\n");
            }

            foreach (var c in before)
                Row(c.frame, c.id, c.p1, c.p2, c.p3, c.p4, c.lat, c.lng, c.alt);
            foreach (var i in flight)
                Row(i.Frame.HasValue ? (byte)i.Frame.Value : frame, (ushort)i.Cmd, i.P1, i.P2, 0, 0, i.Lat, i.Lng, i.Alt);
            return text.ToString();
        }

        /// <summary>Writes each flight to its file in folder, made if need be; returns the files in order.</summary>
        public static List<string> Save(string folder, PointLatLngAlt home, IList<Locationwp> before,
            IList<InspectionFlight> flights, byte frame)
        {
            Directory.CreateDirectory(folder);
            var files = new List<string>();
            for (var k = 0; k < flights.Count; k++)
            {
                var file = Path.Combine(folder, FileName(k + 1, flights.Count));
                File.WriteAllText(file, Waypoints(home, before, flights[k].Items, frame), new UTF8Encoding(false));
                files.Add(file);
            }

            return files;
        }
    }
}
