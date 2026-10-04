using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DOL.Network.Tests
{
    [TestFixture]
    public class UT_SessionIdAllocator
    {
        [Test]
        public void Allocate_ShouldAllocateUniqueSessionIds_Concurrently()
        {
            SessionIdAllocator allocator = new();
            ConcurrentBag<ushort> sessionIds = new();
            int allocationCount = (int) (ushort.MaxValue * 1.5);

            Parallel.For(0, allocationCount, _ =>
            {
                if (allocator.TryAllocate(out ushort sessionId))
                    sessionIds.Add(sessionId);
            });

            List<ushort> uniqueIds = sessionIds.Distinct().ToList();
            Assert.That(sessionIds.Count, Is.EqualTo(uniqueIds.Count));

            foreach (ushort id in uniqueIds)
                Assert.That(id, Is.Positive);
        }

        [Test]
        public void AllocatedCount_ShouldCountIdsAllocatedAndNotYetFreed()
        {
            SessionIdAllocator allocator = new();
            Assert.That(allocator.AllocatedCount, Is.Zero);

            allocator.TryAllocate(out ushort first);
            allocator.TryAllocate(out ushort second);
            allocator.TryAllocate(out _);
            Assert.That(allocator.AllocatedCount, Is.EqualTo(3));

            allocator.Free(first);
            allocator.Free(second);
            Assert.That(allocator.AllocatedCount, Is.EqualTo(1));
        }

        [Test]
        public void AllocatedCount_ShouldIgnoreFreeingAnIdThatIsNotAllocated()
        {
            SessionIdAllocator allocator = new();
            allocator.TryAllocate(out ushort sessionId);

            allocator.Free(sessionId);
            allocator.Free(sessionId);
            allocator.Free(1234);
            Assert.That(allocator.AllocatedCount, Is.Zero);
        }

        [Test]
        public void AllocatedCount_ShouldMatchIdsHeld_Concurrently()
        {
            SessionIdAllocator allocator = new();
            ConcurrentBag<ushort> held = new();

            Parallel.For(0, 20000, i =>
            {
                if (!allocator.TryAllocate(out ushort sessionId))
                    return;

                if (i % 3 == 0)
                    held.Add(sessionId);
                else
                    allocator.Free(sessionId);
            });

            Assert.That(allocator.AllocatedCount, Is.EqualTo(held.Count));
        }

        [Test]
        public void AllocateAndFree_ShouldMaintainUniquePositiveSessionIds_Concurrently()
        {
            SessionIdAllocator allocator = new();
            ConcurrentDictionary<ushort, byte> allocatedIds = new();
            int operationCount = ushort.MaxValue + 1;
            int maxParallelism = Environment.ProcessorCount * 2;
            Random rng = new();

            Parallel.For(0, operationCount, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, i =>
            {
                // Randomly choose to allocate or free.
                if (rng.NextDouble() < 0.6)
                {
                    if (allocator.TryAllocate(out ushort sessionId))
                        allocatedIds.TryAdd(sessionId, 0);
                }
                else
                {
                    // Try to free a random allocated ID, if any.
                    ushort[] current = allocatedIds.Keys.ToArray();

                    if (current.Length > 0)
                    {
                        ushort toFree = current[rng.Next(current.Length)];
                        if (allocatedIds.TryRemove(toFree, out _))
                            allocator.Free(toFree);
                    }
                }
            });

            ushort[] finalIds = allocatedIds.Keys.ToArray();
            Assert.That(finalIds.Length, Is.EqualTo(finalIds.Distinct().Count()), "Duplicate IDs found in final allocation set.");

            foreach (ushort id in finalIds)
                Assert.That(id, Is.Positive, $"ID {id} is not positive.");
        }
    }
}
