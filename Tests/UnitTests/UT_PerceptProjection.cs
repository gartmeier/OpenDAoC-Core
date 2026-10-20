using System.Reflection;
using DOL.GS.Agents;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    [TestFixture]
    public class UT_PerceptProjection
    {
        private static readonly MethodInfo SendUpdatePoints = typeof(IPacketLib).GetMethod(nameof(IPacketLib.SendUpdatePoints));
        private static readonly MethodInfo GetBowPrepare = typeof(IPacketLib).GetProperty(nameof(IPacketLib.BowPrepare)).GetMethod;

        [Test]
        public void Project_ShouldGiveABarePerceptWithOnlyTheCallsName_ForACallWithoutAProjection()
        {
            Percept percept = PerceptProjection.Project(SendUpdatePoints, []);

            Assert.That(percept, Is.EqualTo(new BarePercept("SendUpdatePoints")));
        }

        [Test]
        public void Project_ShouldGiveNothing_ForAPropertyRead()
        {
            Assert.That(PerceptProjection.Project(GetBowPrepare, []), Is.Null);
        }
    }
}
