using System;
using GMap.NET;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using MissionPlanner.Controls;
using MissionPlanner.Utilities;

namespace MissionPlanner.GCSViews
{
    /// <summary>FSEMI delivery on the flight data map: a marker where the payload was released this session.</summary>
    public partial class FlightData
    {
        GMapOverlay deliveryoverlay;

        void InitDelivery()
        {
            deliveryoverlay = new GMapOverlay("delivery");
            gMapControl1.Overlays.Add(deliveryoverlay);

            foreach (var release in DeliveryConfig.Releases)
                AddReleaseMarker(release);
            DeliveryConfig.Released += OnDeliveryReleased;
        }

        void OnDeliveryReleased(DeliveryRelease release)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnDeliveryReleased(release)));
                return;
            }

            AddReleaseMarker(release);
        }

        void AddReleaseMarker(DeliveryRelease release)
        {
            if (release.Lat == 0 && release.Lng == 0)
                return;

            deliveryoverlay.Markers.Add(new GMarkerGoogle(new PointLatLng(release.Lat, release.Lng), GMarkerGoogleType.orange_dot)
            {
                ToolTipText = FsemiText.Format("Delivery_MarkerReleased", release),
                ToolTipMode = MarkerTooltipMode.OnMouseOver
            });
        }
    }
}
