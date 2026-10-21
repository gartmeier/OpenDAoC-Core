using System.Numerics;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_HarnessMovement
    {
        // `PlayerMovementComponent.UpdatePosition` keeps whole units of each coordinate, and the idle position update sends those back.
        // Where a run ends must survive that unchanged, or the idle update is a move that stands the Character up and cancels a quit.
        [TestCase(298900.4f, 642000.9f, 4848.5f)]
        [TestCase(31234.5f, 29123.25f, 2100.75f)]
        [TestCase(296000f, 642000f, 4848f)]
        public void WholeUnits_ShouldBeWhatTheServerKeeps(float x, float y, float z)
        {
            Vector3 sent = HarnessMovement.WholeUnits(new(x, y, z));
            Vector3 repeated = new((int) sent.X, (int) sent.Y, (int) sent.Z);

            Assert.That(repeated, Is.EqualTo(sent));
            Assert.That(Vector3.Distance(sent, new(x, y, z)), Is.LessThan(2));
        }
    }
}
