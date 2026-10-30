using System;
using System.Collections.Generic;
using System.Globalization;
using MissionPlanner.Utilities;

namespace MissionPlanner.Controls
{
    public enum DeliveryMethod
    {
        Auto,
        Gripper,
        Servo
    }

    /// <summary>What the servo output driving the payload shows.</summary>
    public enum PayloadState
    {
        Unknown,
        Locked,
        Released
    }

    /// <summary>A payload release: when and where.</summary>
    public class DeliveryRelease
    {
        public DateTime Time;
        public double Lat;
        public double Lng;
        /// <summary>Altitude in display units, as CurrentState.alt.</summary>
        public double Alt;
        /// <summary>"button" when released from the Delivery tab, "vehicle" when seen on the servo output.</summary>
        public string Source;

        public override string ToString()
        {
            var text = FsemiText.Format("Delivery_Record", Time, Lat, Lng, Alt, CurrentState.DistanceUnit);
            return Source == "vehicle" ? text + "  " + FsemiText.Get("Delivery_RecordByVehicle") : text;
        }
    }

    /// <summary>
    /// How the payload hook is driven, shared by the Delivery tab and the flight planner, and the releases seen
    /// this session. Settings: delivery_method, delivery_servo, delivery_pwm_lock, delivery_pwm_release.
    /// </summary>
    public static class DeliveryConfig
    {
        public const int DefaultServo = 9, DefaultLockPwm = 1100, DefaultReleasePwm = 1900;

        // ArduPilot SERVOn_FUNCTION for the gripper
        const int GripperServoFunction = 28;

        public static DeliveryMethod Method
        {
            get => Enum.TryParse(Settings.Instance["delivery_method"], out DeliveryMethod method) ? method : DeliveryMethod.Auto;
            set => Settings.Instance["delivery_method"] = value.ToString();
        }

        public static int Servo
        {
            get => Settings.Instance.GetInt32("delivery_servo", DefaultServo);
            set => Settings.Instance["delivery_servo"] = value.ToString();
        }

        public static int LockPwm
        {
            get => Settings.Instance.GetInt32("delivery_pwm_lock", DefaultLockPwm);
            set => Settings.Instance["delivery_pwm_lock"] = value.ToString();
        }

        public static int ReleasePwm
        {
            get => Settings.Instance.GetInt32("delivery_pwm_release", DefaultReleasePwm);
            set => Settings.Instance["delivery_pwm_release"] = value.ToString();
        }

        /// <summary>A parameter of the connected vehicle, or null when not known.</summary>
        public static double? Param(string name)
        {
            var param = MainV2.comPort?.MAV?.param;
            if (param == null || !param.ContainsKey(name))
                return null;
            return param[name].Value;
        }

        /// <summary>The method to use: the chosen one, or on Auto the gripper when the vehicle has GRIP_ENABLE set.</summary>
        public static DeliveryMethod Resolve(DeliveryMethod chosen)
        {
            if (chosen != DeliveryMethod.Auto)
                return chosen;
            return Param("GRIP_ENABLE") == 1 ? DeliveryMethod.Gripper : DeliveryMethod.Servo;
        }

        // a servo output this close to a PWM counts as at that PWM
        const int PwmTolerance = 100;

        /// <summary>The state the servo output shows, or Unknown when it can't tell.</summary>
        public static PayloadState ReadPayload(int channel, int lockPwm, int releasePwm)
        {
            var cs = MainV2.comPort?.MAV?.cs;
            if (cs == null || channel < 1)
                return PayloadState.Unknown;

            var output = typeof(CurrentState).GetProperty("ch" + channel + "out")?.GetValue(cs);
            if (!(output is float pwm) || pwm <= 0)
                return PayloadState.Unknown;

            if (releasePwm > 0 && Math.Abs(pwm - releasePwm) <= PwmTolerance)
                return PayloadState.Released;
            if (lockPwm > 0 && Math.Abs(pwm - lockPwm) <= PwmTolerance)
                return PayloadState.Locked;
            return PayloadState.Unknown;
        }

        /// <summary>The payload's state with the saved settings: the gripper's PWMs, or the servo's.</summary>
        public static PayloadState ReadPayload()
        {
            if (Resolve(Method) == DeliveryMethod.Gripper)
                return ReadPayload(GripperServo(), (int)(Param("GRIP_GRAB") ?? 0), (int)(Param("GRIP_RELEASE") ?? 0));
            return ReadPayload(Servo, LockPwm, ReleasePwm);
        }

        /// <summary>The servo output set to the gripper function on the vehicle, or 0.</summary>
        public static int GripperServo()
        {
            for (var i = 1; i <= 16; i++)
                if (Param("SERVO" + i + "_FUNCTION") == GripperServoFunction)
                    return i;
            return 0;
        }

        /// <summary>
        /// The mission command that releases (or locks) the payload: DO_GRIPPER with param2 0 = release,
        /// 1 = grab, or DO_SET_SERVO with the servo and its PWM.
        /// </summary>
        public static (MAVLink.MAV_CMD cmd, double p1, double p2) Command(DeliveryMethod method, bool release, int servo,
            int lockPwm, int releasePwm)
        {
            if (method == DeliveryMethod.Gripper)
                return (MAVLink.MAV_CMD.DO_GRIPPER, 1, release ? 0 : 1);
            return (MAVLink.MAV_CMD.DO_SET_SERVO, servo, release ? releasePwm : lockPwm);
        }

        /// <summary>Whether a mission item releases the payload with the current settings.</summary>
        public static bool IsRelease(ushort id, double p1, double p2)
        {
            if (id == (ushort)MAVLink.MAV_CMD.DO_GRIPPER)
                return p2 == 0;
            if (id == (ushort)MAVLink.MAV_CMD.DO_SET_SERVO)
                return (int)p1 == Servo && (int)p2 == ReleasePwm;
            return false;
        }

        /// <summary>
        /// Speaks an alert when speech is on, in English: a Vietnamese text read by an English Windows voice
        /// can't be understood.
        /// </summary>
        static void Say(string key)
        {
            try
            {
                if (MainV2.speechEnabled())
                    MainV2.speechEngine?.SpeakAsync(FsemiText.Get(key, CultureInfo.GetCultureInfo("en-US")));
            }
            catch (Exception ex)
            {
                // an alert must never take the screen down
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        /// <summary>Whether a mission command flies the vehicle to its position (an ROI has one it doesn't).</summary>
        public static bool IsFlyTo(ushort id)
        {
            switch ((MAVLink.MAV_CMD)id)
            {
                case MAVLink.MAV_CMD.WAYPOINT:
                case MAVLink.MAV_CMD.SPLINE_WAYPOINT:
                case MAVLink.MAV_CMD.LOITER_TIME:
                case MAVLink.MAV_CMD.LOITER_TURNS:
                case MAVLink.MAV_CMD.LOITER_UNLIM:
                case MAVLink.MAV_CMD.LOITER_TO_ALT:
                case MAVLink.MAV_CMD.LAND:
                case MAVLink.MAV_CMD.PAYLOAD_PLACE:
                    return true;
                default:
                    return false;
            }
        }

        static readonly List<DeliveryRelease> releases = new List<DeliveryRelease>();

        /// <summary>Raised on the thread that recorded the release.</summary>
        public static event Action<DeliveryRelease> Released;

        public static IReadOnlyList<DeliveryRelease> Releases => releases;

        public static void Record(DeliveryRelease release)
        {
            releases.Add(release);
            Say("Voice_Released");
            Released?.Invoke(release);
        }
    }
}
