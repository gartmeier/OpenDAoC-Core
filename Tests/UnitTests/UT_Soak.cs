using System.Collections.Generic;
using System.Threading.Tasks;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// `/agent soak` runs login/logout cycles one after another, stops at the first unclean one, and checks the server has as many clients and sessions afterwards as before.
    /// </summary>
    [TestFixture]
    public class UT_Soak
    {
        private static readonly Soak.Usage Idle = new(Clients: 1, Sessions: 1);

        [Test]
        public async Task CleanCycles_ShouldPass()
        {
            List<int> ran = [];

            SoakResult result = await Soak.Run(5, cycle =>
            {
                ran.Add(cycle);
                return Clean();
            }, () => Idle);

            Assert.That(result.Passed, Is.True);
            Assert.That(ran, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public async Task UncleanCycle_ShouldStopTheRunAndBeReported()
        {
            List<int> ran = [];

            SoakResult result = await Soak.Run(5, cycle =>
            {
                ran.Add(cycle);
                return cycle == 3 ? Task.FromResult("Greeen went linkdead.") : Clean();
            }, () => Idle);

            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailedCycle, Is.EqualTo(3));
            Assert.That(result.Reason, Is.EqualTo("Greeen went linkdead."));
            Assert.That(ran, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Cycles_ShouldNeverOverlap()
        {
            List<int> started = [];
            TaskCompletionSource<string> firstCycle = new();

            Task<SoakResult> run = Soak.Run(2, cycle =>
            {
                started.Add(cycle);
                return cycle == 1 ? firstCycle.Task : Clean();
            }, () => Idle);

            Assert.That(started, Is.EqualTo(new[] { 1 }));

            firstCycle.SetResult(null);
            Assert.That(started, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(run.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public async Task CycleThatThrows_ShouldStopTheRunAndBeReported()
        {
            List<int> ran = [];

            SoakResult result = await Soak.Run(3, cycle =>
            {
                ran.Add(cycle);
                return cycle == 2 ? throw new System.InvalidOperationException("No available session IDs.") : Clean();
            }, () => Idle);

            Assert.That(result.FailedCycle, Is.EqualTo(2));
            Assert.That(result.Reason, Does.Contain("No available session IDs."));
            Assert.That(ran, Is.EqualTo(new[] { 1, 2 }));
        }

        [TestCase(2, 1, "1 more client")]
        [TestCase(1, 3, "2 more sessions")]
        [TestCase(3, 3, "2 more clients and 2 more sessions")]
        [TestCase(0, 1, "1 fewer client")]
        public async Task LeakAfterTheRun_ShouldFailIt(int clientsAfter, int sessionsAfter, string difference)
        {
            int measured = 0;

            SoakResult result = await Soak.Run(3, _ => Clean(), () => measured++ == 0 ? Idle : new(clientsAfter, sessionsAfter));

            Assert.That(result.Passed, Is.False);
            Assert.That(result.FailedCycle, Is.Null);
            Assert.That(result.Reason, Does.Contain(difference));
        }

        [Test]
        public async Task UncleanCycle_ShouldNotBeReportedAsALeak()
        {
            int measured = 0;

            SoakResult result = await Soak.Run(3, cycle => cycle == 2 ? Task.FromResult("Greeen hasn't quit.") : Clean(), () => measured++ == 0 ? Idle : new(2, 2));

            Assert.That(result.FailedCycle, Is.EqualTo(2));
            Assert.That(result.Reason, Is.EqualTo("Greeen hasn't quit."));
        }

        private static Task<string> Clean()
        {
            return Task.FromResult<string>(null);
        }
    }
}
