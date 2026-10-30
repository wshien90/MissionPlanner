using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    /// <summary>
    /// Delivery tab: locks and releases the payload with a servo or the ArduPilot gripper, shows whether
    /// it is locked, and keeps a list of where it was released, from this tab or by the vehicle (a mission
    /// or an RC switch, seen on the servo output).
    /// </summary>
    public class DeliveryPanel : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        static readonly Color LockColor = Color.FromArgb(0x1a, 0x73, 0xb8);
        static readonly Color ReleaseColor = Color.FromArgb(0xe5, 0x48, 0x4d);
        static readonly Color LockedColor = Color.FromArgb(0x34, 0xc7, 0x7b);
        static readonly Color ReleasedColor = Color.FromArgb(0xf5, 0x9e, 0x42);
        static readonly Color UnknownColor = Color.FromArgb(0x7b, 0x8a, 0x9a);


        // a release seen on the servo output this soon after one sent from this tab is the same release
        static readonly TimeSpan OwnReleaseWindow = TimeSpan.FromSeconds(5);

        readonly Label title = new Label { Text = FsemiText.Get("Delivery_Title"), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold) };
        readonly Label methodText = new Label { AutoSize = true };
        readonly Pill state = new Pill { Height = 34, Dock = DockStyle.Fill };
        readonly BigButton lockButton = new BigButton { Text = FsemiText.Get("Delivery_Lock"), Color = LockColor, Height = 52, Dock = DockStyle.Fill };
        readonly BigButton releaseButton = new BigButton { Text = FsemiText.Get("Delivery_Release"), Color = ReleaseColor, Height = 52, Dock = DockStyle.Fill };
        readonly Label flightText = new Label { AutoSize = true };
        readonly Label resultText = new Label { AutoSize = true };
        readonly ComboBox methodBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        readonly NumericUpDown servoBox = new NumericUpDown { Minimum = 1, Maximum = 16, Width = 50 };
        readonly NumericUpDown lockPwmBox = new NumericUpDown { Minimum = 800, Maximum = 2200, Increment = 10, Width = 60 };
        readonly NumericUpDown releasePwmBox = new NumericUpDown { Minimum = 800, Maximum = 2200, Increment = 10, Width = 60 };
        readonly ListBox releases = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };

        readonly Timer timer = new Timer { Interval = 500 };
        PayloadState lastCommanded = PayloadState.Unknown;
        // last state read from the servo output, to see releases the vehicle makes itself
        PayloadState lastSeen = PayloadState.Unknown;
        DateTime lastOwnRelease = DateTime.MinValue;
        bool busy;

        public DeliveryPanel()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            Padding = new Padding(6);

            methodBox.Items.AddRange(new object[] { DeliveryMethod.Auto, DeliveryMethod.Gripper, DeliveryMethod.Servo });
            methodBox.SelectedItem = DeliveryConfig.Method;
            servoBox.Value = Clamp(DeliveryConfig.Servo, servoBox);
            lockPwmBox.Value = Clamp(DeliveryConfig.LockPwm, lockPwmBox);
            releasePwmBox.Value = Clamp(DeliveryConfig.ReleasePwm, releasePwmBox);

            methodBox.SelectedIndexChanged += (s, e) => SaveSettings();
            servoBox.ValueChanged += (s, e) => SaveSettings();
            lockPwmBox.ValueChanged += (s, e) => SaveSettings();
            releasePwmBox.ValueChanged += (s, e) => SaveSettings();

            lockButton.Click += (s, e) => Send(PayloadState.Locked);
            releaseButton.Click += (s, e) => Release();

            // sized to its content and docked to the top, so the panel scrolls when the tab is short
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 7,
                BackColor = Color.Transparent
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (var i = 0; i < 7; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            header.Controls.Add(title);
            header.Controls.Add(methodText);
            layout.Controls.Add(header, 0, 0);
            layout.SetColumnSpan(header, 2);

            layout.Controls.Add(state, 0, 1);
            layout.SetColumnSpan(state, 2);

            layout.Controls.Add(lockButton, 0, 2);
            layout.Controls.Add(releaseButton, 1, 2);

            layout.Controls.Add(flightText, 0, 3);
            layout.SetColumnSpan(flightText, 2);
            layout.Controls.Add(resultText, 0, 4);
            layout.SetColumnSpan(resultText, 2);

            var settings = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 6) };
            settings.Controls.Add(Caption(FsemiText.Get("Delivery_Method")));
            settings.Controls.Add(methodBox);
            settings.Controls.Add(Caption(FsemiText.Get("Delivery_Servo")));
            settings.Controls.Add(servoBox);
            settings.Controls.Add(Caption(FsemiText.Get("Delivery_LockPwm")));
            settings.Controls.Add(lockPwmBox);
            settings.Controls.Add(Caption(FsemiText.Get("Delivery_ReleasePwm")));
            settings.Controls.Add(releasePwmBox);
            layout.Controls.Add(settings, 0, 5);
            layout.SetColumnSpan(settings, 2);

            var history = new Panel { Dock = DockStyle.Fill, Height = 110, BackColor = Color.Transparent };
            history.Controls.Add(releases);
            history.Controls.Add(new Label { Text = FsemiText.Get("Delivery_Releases"), Dock = DockStyle.Top, Height = 18 });
            layout.Controls.Add(history, 0, 6);
            layout.SetColumnSpan(history, 2);

            Controls.Add(layout);

            foreach (var release in DeliveryConfig.Releases)
                releases.Items.Insert(0, release);
            DeliveryConfig.Released += OnReleased;

            timer.Tick += (s, e) => UpdateState();
            timer.Start();
            UpdateState();
        }

        static Label Caption(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 6, 0, 0) };
        }

        static decimal Clamp(int value, NumericUpDown box)
        {
            return Math.Max(box.Minimum, Math.Min(box.Maximum, value));
        }

        void SaveSettings()
        {
            DeliveryConfig.Method = (DeliveryMethod)(methodBox.SelectedItem ?? DeliveryMethod.Auto);
            DeliveryConfig.Servo = (int)servoBox.Value;
            DeliveryConfig.LockPwm = (int)lockPwmBox.Value;
            DeliveryConfig.ReleasePwm = (int)releasePwmBox.Value;
            UpdateState();
        }

        void OnReleased(DeliveryRelease release)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnReleased(release)));
                return;
            }

            releases.Items.Insert(0, release);
        }

        static bool Connected => MainV2.comPort?.BaseStream != null && MainV2.comPort.BaseStream.IsOpen;

        DeliveryMethod ActiveMethod => DeliveryConfig.Resolve((DeliveryMethod)(methodBox.SelectedItem ?? DeliveryMethod.Auto));

        /// <summary>The servo output driving the payload: the one set to the gripper function (28) in gripper mode.</summary>
        int ServoChannel
        {
            get
            {
                return ActiveMethod == DeliveryMethod.Gripper ? DeliveryConfig.GripperServo() : (int)servoBox.Value;
            }
        }

        int LockPwm => ActiveMethod == DeliveryMethod.Gripper ? (int)(DeliveryConfig.Param("GRIP_GRAB") ?? 0) : (int)lockPwmBox.Value;

        int ReleasePwm => ActiveMethod == DeliveryMethod.Gripper ? (int)(DeliveryConfig.Param("GRIP_RELEASE") ?? 0) : (int)releasePwmBox.Value;

        /// <summary>The state the servo output shows, or Unknown when it can't tell.</summary>
        PayloadState ReadServoState()
        {
            return DeliveryConfig.ReadPayload(ServoChannel, LockPwm, ReleasePwm);
        }

        static DeliveryRelease ReleaseHere(string source)
        {
            var cs = MainV2.comPort.MAV.cs;
            return new DeliveryRelease { Time = DateTime.Now, Lat = cs.lat, Lng = cs.lng, Alt = cs.alt, Source = source };
        }

        void UpdateState()
        {
            var method = ActiveMethod;
            var channel = ServoChannel;
            releases.BackColor = ThemeManager.ControlBGColor;
            releases.ForeColor = ThemeManager.TextColor;
            servoBox.Enabled = lockPwmBox.Enabled = releasePwmBox.Enabled = method == DeliveryMethod.Servo;
            methodText.Text = method == DeliveryMethod.Gripper
                ? (channel > 0 ? FsemiText.Format("Delivery_GripperOnServo", channel) : FsemiText.Get("Delivery_Gripper"))
                : FsemiText.Format("Delivery_ServoN", channel);

            if (!Connected)
            {
                state.Text = FsemiText.Get("Delivery_NotConnected");
                state.Color = UnknownColor;
                flightText.Text = "";
                lockButton.Enabled = releaseButton.Enabled = false;
                lastSeen = PayloadState.Unknown;
                return;
            }

            var seen = ReadServoState();
            // locked to released on the servo output, not from this tab: a mission or RC release
            if (lastSeen == PayloadState.Locked && seen == PayloadState.Released &&
                DateTime.Now - lastOwnRelease > OwnReleaseWindow)
            {
                var release = ReleaseHere("vehicle");
                log.Info("Payload released by the vehicle at " + release);
                DeliveryConfig.Record(release);
            }

            if (seen != PayloadState.Unknown)
                lastSeen = seen;

            var current = seen != PayloadState.Unknown ? seen : lastCommanded;
            state.Text = FsemiText.Get(current == PayloadState.Locked ? "Delivery_Locked" :
                current == PayloadState.Released ? "Delivery_Released" : "Delivery_Unknown");
            state.Color = current == PayloadState.Locked ? LockedColor : current == PayloadState.Released ? ReleasedColor : UnknownColor;

            var cs = MainV2.comPort.MAV.cs;
            flightText.Text = FsemiText.Format("Delivery_Flight", cs.alt, CurrentState.DistanceUnit, cs.groundspeed,
                CurrentState.SpeedUnit, FsemiText.Get(cs.armed ? "Common_Armed" : "Common_Disarmed"));
            lockButton.Enabled = releaseButton.Enabled = !busy;
        }

        void Release()
        {
            var cs = MainV2.comPort.MAV.cs;
            var text = FsemiText.Format("Delivery_Confirm", cs.alt, CurrentState.DistanceUnit, cs.groundspeed,
                CurrentState.SpeedUnit);
            if (!cs.armed)
                text += "\n" + FsemiText.Get("Delivery_ConfirmDisarmed");
            if (CustomMessageBox.Show(text, FsemiText.Get("Delivery_ConfirmTitle"), CustomMessageBox.MessageBoxButtons.YesNo,
                    CustomMessageBox.MessageBoxIcon.Warning, FsemiText.Get("Delivery_ConfirmYes"),
                    FsemiText.Get("Common_Cancel")) != CustomMessageBox.DialogResult.Yes)
                return;

            Send(PayloadState.Released);
        }

        void Send(PayloadState target)
        {
            if (!Connected || busy)
                return;

            var port = MainV2.comPort;
            var method = ActiveMethod;
            var channel = (int)servoBox.Value;
            var pwm = target == PayloadState.Released ? (int)releasePwmBox.Value : (int)lockPwmBox.Value;
            var here = ReleaseHere("button");
            var action = target == PayloadState.Released ? "Release" : "Lock";

            busy = true;
            var releasing = target == PayloadState.Released;
            resultText.Text = FsemiText.Get(releasing ? "Delivery_Releasing" : "Delivery_Locking");
            // the servo output can show the release before the reply arrives
            if (target == PayloadState.Released)
                lastOwnRelease = DateTime.Now;
            UpdateState();

            Task.Run(() =>
            {
                try
                {
                    // DO_GRIPPER: param2 0 = release, 1 = grab
                    return method == DeliveryMethod.Gripper
                        ? port.doCommand(port.sysidcurrent, (byte)port.compidcurrent, MAVLink.MAV_CMD.DO_GRIPPER, 1,
                            target == PayloadState.Released ? 0 : 1, 0, 0, 0, 0, 0)
                        : port.doCommand(port.sysidcurrent, (byte)port.compidcurrent, MAVLink.MAV_CMD.DO_SET_SERVO, channel,
                            pwm, 0, 0, 0, 0, 0);
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                    return false;
                }
            }).ContinueWith(t =>
            {
                if (IsDisposed)
                    return;
                BeginInvoke((Action)(() =>
                {
                    busy = false;
                    if (t.Result)
                    {
                        lastCommanded = target;
                        resultText.Text = FsemiText.Format(releasing ? "Delivery_ReleaseSent" : "Delivery_LockSent",
                            DateTime.Now.ToString("HH:mm:ss"));
                        log.Info("Payload " + action + " at " + here);
                        if (target == PayloadState.Released)
                        {
                            lastOwnRelease = DateTime.Now;
                            DeliveryConfig.Record(here);
                        }
                    }
                    else
                    {
                        resultText.Text = FsemiText.Get(releasing ? "Delivery_ReleaseFailed" : "Delivery_LockFailed");
                    }

                    UpdateState();
                }));
            });
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(ThemeManager.BGColor);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DeliveryConfig.Released -= OnReleased;
                timer.Stop();
                timer.Dispose();
            }

            base.Dispose(disposing);
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Rounded status pill with centered white text.</summary>
        class Pill : Control
        {
            Color color = UnknownColor;

            public Color Color
            {
                get => color;
                set
                {
                    if (color == value)
                        return;
                    color = value;
                    Invalidate();
                }
            }

            public Pill()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Font = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
                Margin = new Padding(3, 3, 3, 6);
            }

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(ThemeManager.BGColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = Rounded(r, 8))
                using (var fill = new SolidBrush(Enabled ? color : Color.FromArgb(0x55, color)))
                using (var text = new SolidBrush(Color.White))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.FillPath(fill, path);
                    g.DrawString(Text, Font, text, r, sf);
                }
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                Invalidate();
            }
        }

        /// <summary>Large, colored button for gloved or touch use; not themed.</summary>
        class BigButton : Pill
        {
            bool pressed;

            public BigButton()
            {
                Font = new Font(FontFamily.GenericSansSerif, 12f, FontStyle.Bold);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                pressed = true;
                Invalidate();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                pressed = false;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (pressed && Enabled)
                    using (var shade = new SolidBrush(Color.FromArgb(60, Color.Black)))
                    using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 8))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.FillPath(shade, path);
                    }
            }
        }
    }
}
