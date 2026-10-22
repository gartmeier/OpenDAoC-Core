using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DOL.GS.Agents;
using NUnit.Framework;

namespace DOL.GS.Tests
{
    /// <summary>
    /// The Harness runs an Agent's Character along a route the way the real 1.127 client in `Fixtures/Agents/v1127/run-turn-stop.tsv` runs. These runs are straight lines, the shape of every leg of a route.
    /// </summary>
    [TestFixture]
    public class UT_RouteRun
    {
        private const float MAX_SPEED = GamePlayer.PLAYER_BASE_SPEED;

        // A game loop running at about 30 ticks per second.
        private const int TICK_DURATION = 33;

        // Two zones side by side along X, as in a region's static layout.
        private static readonly Region Region = WorldDoubles.Region(100, (101, 0, 0, 65536, 65536), (102, 65536, 0, 65536, 65536));

        private static readonly Vector3 Start = new(30000, 30000, 2000);

        [Test]
        public void At_ShouldArriveExactlyAtTheTarget()
        {
            Vector3 target = new(31234.5f, 29123.25f, 2100);
            RouteRun run = Run(Start, target);

            RouteRun.Step last = Steps(run).Last();

            Assert.That(last.Position, Is.EqualTo(target));
            Assert.That(last.Speed, Is.Zero);
            Assert.That(last.Ended, Is.True);
            Assert.That(last.Arrived, Is.True);
        }

        // Heading 0 points towards +Y and grows clockwise, as in the capture: the first run went (−29.3, −28.3) per update at heading 1523.
        [TestCase(0, 1000, 0)]
        [TestCase(-1000, 0, 1024)]
        [TestCase(0, -1000, 2048)]
        [TestCase(1000, 0, 3072)]
        [TestCase(-2930, -2830, 1523)]
        public void Heading_ShouldPointAtTheTarget(float dx, float dy, int heading)
        {
            RouteRun run = Run(Start, Start + new Vector3(dx, dy, 0));

            Assert.That((int) run.At(0).Heading, Is.EqualTo(heading).Within(1));
            Assert.That(Steps(run).Select(static step => (int) step.Heading), Is.All.EqualTo(run.At(0).Heading));
        }

        [Test]
        public void Heading_ShouldStayAsItWas_ForATargetStraightAbove()
        {
            RouteRun run = new(Start, Start + new Vector3(0, 0, 100), 1234, 0, MAX_SPEED, Region, 0);

            Assert.That(run.At(0).Heading, Is.EqualTo(1234));
            Assert.That(run.At(0).Arrived, Is.True);
        }

        [Test]
        public void At_ShouldInterpolateZLinearlyAlongTheRun()
        {
            Vector3 target = Start + new Vector3(2000, 0, 400);
            RouteRun run = Run(Start, target);

            foreach (RouteRun.Step step in Steps(run))
            {
                float fraction = (step.Position.X - Start.X) / 2000;
                Assert.That(step.Position.Z, Is.EqualTo(Start.Z + 400 * fraction).Within(0.01));
            }
        }

        [Test]
        public void At_ShouldNotOvershoot_AfterAStall()
        {
            Vector3 target = Start + new Vector3(500, 500, 0);
            RouteRun run = Run(Start, target);

            // The game loop stalls for an hour after the first tick.
            run.At(TICK_DURATION);
            RouteRun.Step step = run.At(60 * 60 * 1000);

            Assert.That(step.Position, Is.EqualTo(target));
            Assert.That(step.Arrived, Is.True);
        }

        [Test]
        public void At_ShouldNeverPassTheTarget()
        {
            Vector3 target = Start + new Vector3(-300, 700, 0);
            RouteRun run = Run(Start, target);

            for (long now = 0; now < 10000; now += 7)
                Assert.That(Flat(run.At(now).Position - Start), Is.LessThanOrEqualTo(Flat(target - Start)));
        }

        [Test]
        public void Stop_ShouldEndTheRunWhereTheCharacterIs()
        {
            Vector3 target = Start + new Vector3(3000, 0, 0);
            RouteRun run = Run(Start, target);
            Vector3 there = run.At(2000).Position;

            run.Stop(2000);

            foreach (long now in new long[] { 2000, 2033, 60000 })
            {
                RouteRun.Step step = run.At(now);
                Assert.That(step.Position, Is.EqualTo(there));
                Assert.That(step.Speed, Is.Zero);
                Assert.That(step.Ended, Is.True);
                Assert.That(step.Arrived, Is.False);
            }

            Assert.That(run.IsStopped, Is.True);
            Assert.That(there.X, Is.GreaterThan(Start.X).And.LessThan(target.X));
        }

        [Test]
        public void Stop_ShouldChangeNothing_OnceArrived()
        {
            Vector3 target = Start + new Vector3(100, 0, 0);
            RouteRun run = Run(Start, target);

            run.Stop(60000);

            Assert.That(run.At(60000).Arrived, Is.True);
            Assert.That(run.IsStopped, Is.False);
        }

        [Test]
        public void ZoneId_ShouldChange_AtAZoneBorderWithinTheRegion()
        {
            Vector3 start = new(65000, 30000, 2000);
            RouteRun run = Run(start, start + new Vector3(1000, 0, 0));

            foreach (RouteRun.Step step in Steps(run))
                Assert.That(step.ZoneId, Is.EqualTo(step.Position.X < 65536 ? 101 : 102));

            Assert.That(run.At(0).ZoneId, Is.EqualTo(101));
            Assert.That(Steps(run).Last().ZoneId, Is.EqualTo(102));
        }

        [Test]
        public void ZoneId_ShouldStayTheStarts_OutsideEveryZone()
        {
            Vector3 start = new(130000, 30000, 2000);
            RouteRun run = Run(start, start + new Vector3(2000, 0, 0));

            Assert.That(Steps(run).Last().ZoneId, Is.EqualTo(102));
        }

        // From standing, both captured runs gained about 53 units per second every 200 ms until they reached 191.
        [Test]
        public void Speed_ShouldRampUpAsTheCapturedClientsDoes()
        {
            RouteRun run = Run(Start, Start + new Vector3(5000, 0, 0));

            Assert.That(run.At(200).Speed - run.At(0).Speed, Is.EqualTo(53).Within(2));
            Assert.That(run.At(400).Speed - run.At(200).Speed, Is.EqualTo(53).Within(2));
            Assert.That(run.At(600).Speed - run.At(400).Speed, Is.EqualTo(53).Within(2));
            Assert.That(run.At(900).Speed, Is.EqualTo(MAX_SPEED));
            Assert.That(run.At(5000).Speed, Is.EqualTo(MAX_SPEED));
        }

        // Both captured stops: one update with the speed falling to about 100, then one with speed 0, about 40 units on.
        [Test]
        public void Speed_ShouldFallToZeroAtTheTargetAsTheCapturedClientsDoes()
        {
            Vector3 target = Start + new Vector3(5000, 0, 0);
            RouteRun run = Run(Start, target);
            long arrival = Enumerable.Range(0, 100000).Select(static t => (long) t).First(now => run.At(now).Arrived);

            Assert.That(run.At(arrival - 400).Speed, Is.EqualTo(MAX_SPEED).Within(5));
            Assert.That(run.At(arrival - 200).Speed, Is.EqualTo(100).Within(10));
            Assert.That(target.X - run.At(arrival - 400).Position.X, Is.EqualTo(40).Within(3));
        }

        // The server's speed check measures how far the Character went in X and Y between two position updates. It allows 1.15 times the maximum speed.
        [Test]
        public void Distance_ShouldNeverExceedTheMaximumSpeed_BetweenTwoUpdates([Values(33, 200, 233)] int interval)
        {
            RouteRun run = Run(Start, Start + new Vector3(4000, -1500, 0));
            Vector3 previous = run.At(0).Position;

            for (long now = interval; now < 30000; now += interval)
            {
                Vector3 position = run.At(now).Position;
                Assert.That(Flat(position - previous), Is.LessThanOrEqualTo(MAX_SPEED * interval / 1000f + 0.1f));
                previous = position;
            }
        }

        [Test]
        public void At_ShouldArriveExactly_OnARunTooShortToReachTheMaximumSpeed()
        {
            Vector3 target = Start + new Vector3(30, 0, 0);
            RouteRun run = Run(Start, target);

            List<RouteRun.Step> steps = Steps(run);

            Assert.That(steps.Max(static step => step.Speed), Is.LessThan(MAX_SPEED));
            Assert.That(steps.Last().Position, Is.EqualTo(target));
            Assert.That(steps.Last().Arrived, Is.True);
        }

        // A new run that replaces one keeps the speed the Character has.
        [Test]
        public void At_ShouldStartAtTheGivenSpeed()
        {
            RouteRun run = new(Start, Start + new Vector3(0, 3000, 0), 0, MAX_SPEED, MAX_SPEED, Region, 0);

            Assert.That(run.At(0).Speed, Is.EqualTo(MAX_SPEED));
            Assert.That(run.At(200).Position.Y - Start.Y, Is.EqualTo(MAX_SPEED / 5).Within(0.1));
        }

        [Test]
        public void At_ShouldNeverPassTheTarget_WhenStartingTooFastToStopInTime()
        {
            Vector3 target = Start + new Vector3(0, 5, 0);
            RouteRun run = new(Start, target, 0, MAX_SPEED, MAX_SPEED, Region, 0);

            List<RouteRun.Step> steps = Steps(run);

            Assert.That(steps.Select(static step => step.Position.Y), Is.All.LessThanOrEqualTo(target.Y));
            Assert.That(steps.Last().Position, Is.EqualTo(target));
        }

        [Test]
        public void ChangeMaxSpeed_ShouldDropTheSpeedAtOnce_WhenLower()
        {
            RouteRun run = Run(Start, Start + new Vector3(5000, 0, 0));

            run.ChangeMaxSpeed(100, 2000);

            Assert.That(run.At(2000).Speed, Is.EqualTo(100));
            Assert.That(run.At(2200).Position.X - run.At(2000).Position.X, Is.EqualTo(20).Within(0.1));
        }

        [Test]
        public void ChangeMaxSpeed_ShouldSpeedUpToIt_WhenHigher()
        {
            RouteRun run = Run(Start, Start + new Vector3(8000, 0, 0));

            run.ChangeMaxSpeed(223, 2000);

            Assert.That(run.At(2000).Speed, Is.EqualTo(MAX_SPEED));
            Assert.That(run.At(3000).Speed, Is.EqualTo(223));
            Assert.That(Steps(run).Last().Arrived, Is.True);
        }

        // A rooted Character holding its run key goes nowhere, and the run doesn't end.
        [Test]
        public void At_ShouldWaitWhereItIs_WithAMaximumSpeedOfZero()
        {
            RouteRun run = new(Start, Start + new Vector3(1000, 0, 0), 0, 0, 0, Region, 0);

            Assert.That(run.At(60000).Position, Is.EqualTo(Start));
            Assert.That(run.At(60000).Ended, Is.False);
        }

        private static RouteRun Run(Vector3 start, Vector3 target)
        {
            return new(start, target, 0, 0, MAX_SPEED, Region, 0);
        }

        // Every tick of a game loop running from 0, up to the one where the run ends.
        private static List<RouteRun.Step> Steps(RouteRun run)
        {
            List<RouteRun.Step> steps = [];

            for (long now = 0; now < 10 * 60 * 1000; now += TICK_DURATION)
            {
                RouteRun.Step step = run.At(now);
                steps.Add(step);

                if (step.Ended)
                    return steps;
            }

            throw new InvalidOperationException("The run didn't end.");
        }

        private static float Flat(Vector3 offset)
        {
            return new Vector2(offset.X, offset.Y).Length();
        }
    }
}
