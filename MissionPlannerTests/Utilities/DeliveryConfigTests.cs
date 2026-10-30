using Microsoft.VisualStudio.TestTools.UnitTesting;
using MissionPlanner.Controls;

namespace MissionPlanner.Utilities.Tests
{
    [TestClass]
    public class DeliveryConfigTests
    {
        [TestMethod]
        public void ReleaseAndLockCommands()
        {
            // DO_GRIPPER: param1 the gripper, param2 0 = release, 1 = grab
            Assert.AreEqual((MAVLink.MAV_CMD.DO_GRIPPER, 1.0, 0.0),
                DeliveryConfig.Command(DeliveryMethod.Gripper, true, 9, 1100, 1900));
            Assert.AreEqual((MAVLink.MAV_CMD.DO_GRIPPER, 1.0, 1.0),
                DeliveryConfig.Command(DeliveryMethod.Gripper, false, 9, 1100, 1900));
            // DO_SET_SERVO: param1 the servo, param2 its PWM
            Assert.AreEqual((MAVLink.MAV_CMD.DO_SET_SERVO, 10.0, 1900.0),
                DeliveryConfig.Command(DeliveryMethod.Servo, true, 10, 1100, 1900));
            Assert.AreEqual((MAVLink.MAV_CMD.DO_SET_SERVO, 10.0, 1100.0),
                DeliveryConfig.Command(DeliveryMethod.Servo, false, 10, 1100, 1900));
        }
    }
}
