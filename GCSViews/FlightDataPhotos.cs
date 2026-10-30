using System;
using System.Windows.Forms;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews
{
    /// <summary>FSEMI inspection photos: records where photos are taken, and Inspection photos on the map menu.</summary>
    public partial class FlightData
    {
        void InitPhotos()
        {
            PhotoRecorder.Start();

            var photos = new ToolStripMenuItem(FsemiText.Get("Photo_Title"));
            photos.Click += (s, e) =>
            {
                using (var form = new InspectionPhotosForm())
                    form.ShowDialog(this);
            };
            contextMenuStripMap.Items.Add(photos);
        }
    }
}
