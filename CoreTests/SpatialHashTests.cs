using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WormRush.Core.Tests
{
    public class SpatialHashTests
    {
        [Test]
        public void QueryReturnsEveryPointWithinRadiusAndNothingFarAway()
        {
            const float cell = 48f;
            SpatialHash hash = new SpatialHash(cell, 1000f);
            SimRandom rng = new SimRandom(11);
            float[] xs = new float[600];
            float[] ys = new float[600];
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] = rng.Range(-900f, 900f);
                ys[i] = rng.Range(-900f, 900f);
                hash.Insert(xs[i], ys[i], i);
            }

            List<int> found = new List<int>();
            for (int q = 0; q < 100; q++)
            {
                float qx = rng.Range(-950f, 950f);
                float qy = rng.Range(-950f, 950f);
                float r = rng.Range(5f, 120f);
                hash.Query(qx, qy, r, found);

                HashSet<int> foundSet = new HashSet<int>(found);
                for (int i = 0; i < xs.Length; i++)
                {
                    float dx = xs[i] - qx;
                    float dy = ys[i] - qy;
                    if (dx * dx + dy * dy <= r * r)
                        Assert.That(foundSet.Contains(i), "missed point " + i);
                    if (MathF.Abs(dx) > r + cell || MathF.Abs(dy) > r + cell)
                        Assert.That(foundSet.Contains(i), Is.False, "returned a far point " + i);
                }
            }
        }

        [Test]
        public void ClearEmptiesTheHash()
        {
            SpatialHash hash = new SpatialHash(48f, 1000f);
            hash.Insert(0f, 0f, 1);
            hash.Clear();

            List<int> found = new List<int>();
            hash.Query(0f, 0f, 100f, found);

            Assert.That(hash.Count, Is.EqualTo(0));
            Assert.That(found, Is.Empty);
        }

        [Test]
        public void PointsOutsideTheGridAreStillFound()
        {
            SpatialHash hash = new SpatialHash(10f, 100f);
            hash.Insert(5000f, 0f, 7);

            List<int> found = new List<int>();
            hash.Query(150f, 0f, 10f, found);

            Assert.That(found, Does.Contain(7));
        }

        [Test]
        public void GrowsPastItsInitialCapacity()
        {
            SpatialHash hash = new SpatialHash(48f, 1000f, 4);
            for (int i = 0; i < 5000; i++) hash.Insert(i % 100, i % 37, i);

            Assert.That(hash.Count, Is.EqualTo(5000));
        }
    }
}
