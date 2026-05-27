using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using C2VGeometry;

namespace Code2Viz.Tests;

// These tests construct the C2VGeometry RayCaster from an explicit collection
// of shapes — there is NO canvas / singleton dependency, so they are
// parallel-safe and deliberately NOT placed in the "CanvasState" collection.
//
// We force Shape.DefaultRegistry = null so that constructing the test shapes
// (which auto-register by default) does not try to push them onto any
// registry. The RayCaster is always built from the explicit list we hand it.
public class C2VRayCasterTests
{
    public C2VRayCasterTests()
    {
        // No registry => shapes are created standalone, never registered.
        Shape.DefaultRegistry = null;
    }

    [Fact]
    public void FindIntersection_HitsClosestOfTwoCircles()
    {
        var near = new VCircle(10, 0, 1);
        var far  = new VCircle(50, 0, 1);

        var rc = new RayCaster(new Shape[] { near, far });
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(near, hit!.Value.Shape);
        Assert.Equal(9.0, hit.Value.Point.X, 6);
        Assert.Equal(0.0, hit.Value.Point.Y, 6);
        Assert.Equal(9.0, hit.Value.Distance, 6);
    }

    [Fact]
    public void FindIntersection_HitsCircle()
    {
        var circle = new VCircle(10, 0, 1);

        var rc = new RayCaster(new Shape[] { circle });
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(circle, hit!.Value.Shape);
        Assert.Equal(9.0, hit.Value.Point.X, 6);
    }

    [Fact]
    public void FindIntersection_HitsLineSegment()
    {
        var line = new VLine(5, -5, 5, 5);

        var rc = new RayCaster(new Shape[] { line });
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(line, hit!.Value.Shape);
        Assert.Equal(5.0, hit.Value.Point.X, 6);
        Assert.Equal(0.0, hit.Value.Point.Y, 6);
        Assert.Equal(5.0, hit.Value.Distance, 6);
    }

    [Fact]
    public void FindIntersection_MissesWhenRayPointsAway()
    {
        var circle = new VCircle(10, 0, 1);

        var rc = new RayCaster(new Shape[] { circle });
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(-1, 0, 0));

        Assert.Null(hit);
    }

    [Fact]
    public void FindIntersection_ReturnsNullForDegenerateDirection()
    {
        var circle = new VCircle(0, 0, 1);

        var rc = new RayCaster(new Shape[] { circle });
        Assert.Null(rc.FindIntersection(new VXYZ(5, 0, 0), new VXYZ(0, 0, 0)));
    }

    [Fact]
    public void Constructor_HandlesEmptyCollection()
    {
        var rc = new RayCaster(Array.Empty<Shape>());
        Assert.Equal(0, rc.Count);
        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
    }

    [Fact]
    public void Constructor_ExcludesVPoints()
    {
        // VPoint markers are zero-area visual labels — unconditionally skipped.
        var p1 = new VPoint(10, 0);
        var p2 = new VPoint(20, 0);

        var rc = new RayCaster(new Shape[] { p1, p2 });
        Assert.Equal(0, rc.Count);
        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
    }

    [Fact]
    public void Constructor_ExcludesHiddenShapes()
    {
        var visible = new VCircle(10, 0, 1);
        var hidden  = new VCircle(5, 0, 1);
        hidden.Hide();

        var rc = new RayCaster(new Shape[] { visible, hidden });
        Assert.Equal(1, rc.Count);

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Same(visible, hit!.Value.Shape);
        Assert.Equal(9.0, hit.Value.Point.X, 6);
    }

    [Fact]
    public void Constructor_ExcludesVPointsMixedWithRealShapes()
    {
        var circle = new VCircle(10, 0, 1);
        var marker = new VPoint(10, 0);

        var rc = new RayCaster(new Shape[] { marker, circle });
        Assert.Equal(1, rc.Count);

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Same(circle, hit!.Value.Shape);
        Assert.Equal(9.0, hit.Value.Point.X, 6);
    }

    [Fact]
    public void FindIntersection_PrunesToCorrectShapeAmongMany()
    {
        var shapes = new List<Shape>();
        for (int x = 0; x < 50; x++)
        for (int y = 0; y < 50; y++)
            shapes.Add(new VCircle(x * 10, y * 10, 0.4));
        // A single circle on the off-grid row (y=7) — the only shape the ray meets.
        var onlyTarget = new VCircle(300, 7, 0.4);
        shapes.Add(onlyTarget);

        var rc = new RayCaster(shapes);
        var hit = rc.FindIntersection(new VXYZ(-5, 7, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(onlyTarget, hit!.Value.Shape);
        Assert.Equal(299.6, hit.Value.Point.X, 3);
    }

    [Fact]
    public void FindIntersection_RespectsArcAngleRange()
    {
        var upper = new VArc(0, 0, 5, 0, 180);

        var rc = new RayCaster(new Shape[] { upper });

        var up = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(0, 1, 0));
        Assert.NotNull(up);
        Assert.Same(upper, up!.Value.Shape);
        Assert.Equal(5.0, up.Value.Point.Y, 6);

        var down = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(0, -1, 0));
        Assert.Null(down);
    }

    [Fact]
    public void FindIntersection_HitsEllipseAtMinorAxis()
    {
        var ellipse = new VEllipse(0, 0, 10, 3);

        var rc = new RayCaster(new Shape[] { ellipse });
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(0, 1, 0));

        Assert.NotNull(hit);
        Assert.Same(ellipse, hit!.Value.Shape);
        Assert.Equal(3.0, hit.Value.Point.Y, 6);
    }

    [Fact]
    public void FindIntersection_HitsRectangleBoundary()
    {
        var rect = new VRectangle(10, -5, 10, 10);

        var rc = new RayCaster(new Shape[] { rect });
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Equal(10.0, hit!.Value.Point.X, 6);
        Assert.Equal(0.0, hit.Value.Point.Y, 6);
    }

    // -- maxDistance ----------------------------------------------------------

    [Fact]
    public void FindIntersection_MaxDistance_RejectsFarHits()
    {
        var circle = new VCircle(100, 0, 1);
        var rc = new RayCaster(new Shape[] { circle });

        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 50));
        Assert.NotNull(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 200));
    }

    [Fact]
    public void FindIntersection_MaxDistance_PicksNearestWithinRange()
    {
        var near = new VCircle(10, 0, 1);
        var far  = new VCircle(100, 0, 1);
        var rc = new RayCaster(new Shape[] { near, far });

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 50);
        Assert.NotNull(hit);
        Assert.Same(near, hit!.Value.Shape);
    }

    // -- Exclusion list -------------------------------------------------------

    [Fact]
    public void FindIntersection_SkipsExcludedShape_AndPicksNextClosest()
    {
        var near = new VCircle(10, 0, 1);
        var far  = new VCircle(50, 0, 1);
        var rc = new RayCaster(new Shape[] { near, far });

        var hit = rc.FindIntersection(
            new VXYZ(0, 0, 0), new VXYZ(1, 0, 0),
            exclusionList: new List<Shape> { near });

        Assert.NotNull(hit);
        Assert.Same(far, hit!.Value.Shape);
        Assert.Equal(49.0, hit.Value.Point.X, 6);
    }

    [Fact]
    public void FindIntersection_ReturnsNullWhenAllCandidatesAreExcluded()
    {
        var c1 = new VCircle(10, 0, 1);
        var c2 = new VCircle(50, 0, 1);
        var rc = new RayCaster(new Shape[] { c1, c2 });

        var hit = rc.FindIntersection(
            new VXYZ(0, 0, 0), new VXYZ(1, 0, 0),
            exclusionList: new List<Shape> { c1, c2 });

        Assert.Null(hit);
    }

    // -- HasIntersection (any-hit) -------------------------------------------

    [Fact]
    public void HasIntersection_ReturnsTrueWhenAnyShapeIsHit()
    {
        var circle = new VCircle(10, 0, 1);
        var rc = new RayCaster(new Shape[] { circle });

        Assert.True(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
        Assert.False(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(-1, 0, 0)));
    }

    [Fact]
    public void HasIntersection_RespectsMaxDistance()
    {
        var circle = new VCircle(100, 0, 1);
        var rc = new RayCaster(new Shape[] { circle });

        Assert.False(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 50));
        Assert.True(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 200));
    }

    // -- Batch FindIntersections ---------------------------------------------

    [Fact]
    public void FindIntersections_BatchReturnsResultsAlignedWithInput()
    {
        var c1 = new VCircle(10, 0, 1);
        var c2 = new VCircle(0, 10, 1);
        var rc = new RayCaster(new Shape[] { c1, c2 });

        var queries = new[]
        {
            new RayQuery(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)), // hits c1
            new RayQuery(new VXYZ(0, 0, 0), new VXYZ(0, 1, 0)), // hits c2
            new RayQuery(new VXYZ(0, 0, 0), new VXYZ(-1, 0, 0)) // miss
        };

        var results = rc.FindIntersections(queries);
        Assert.Equal(3, results.Length);
        Assert.Same(c1, results[0]!.Value.Shape);
        Assert.Same(c2, results[1]!.Value.Shape);
        Assert.Null(results[2]);
    }

    [Fact]
    public void FindIntersections_ParallelMatchesSequential()
    {
        var rng = new Random(42);
        var shapes = new List<Shape>();
        for (int i = 0; i < 200; i++)
        {
            double x = rng.NextDouble() * 100 - 50;
            double y = rng.NextDouble() * 100 - 50;
            shapes.Add(new VCircle(x, y, 0.5 + rng.NextDouble()));
        }
        var rc = new RayCaster(shapes);

        var queries = Enumerable.Range(0, 500)
            .Select(_ => new RayQuery(
                new VXYZ(rng.NextDouble() * 100 - 50, rng.NextDouble() * 100 - 50, 0),
                new VXYZ(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, 0)))
            .ToList();

        var seq = rc.FindIntersections(queries, parallel: false);
        var par = rc.FindIntersections(queries, parallel: true);

        Assert.Equal(seq.Length, par.Length);
        for (int i = 0; i < seq.Length; i++)
        {
            Assert.Equal(seq[i].HasValue, par[i].HasValue);
            if (seq[i].HasValue)
            {
                Assert.Same(seq[i]!.Value.Shape, par[i]!.Value.Shape);
                Assert.Equal(seq[i]!.Value.Distance, par[i]!.Value.Distance, 9);
            }
        }
    }

    // -- Refit ----------------------------------------------------------------

    [Fact]
    public void Refit_PicksUpShapeMovement()
    {
        var circle = new VCircle(10, 0, 1);
        var rc = new RayCaster(new Shape[] { circle });

        var hitBefore = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.Equal(9.0, hitBefore!.Value.Point.X, 6);

        // VXYZ is immutable in C2VGeometry — move the circle's center via Move.
        circle.Move(new VXYZ(10, 0, 0)); // center 10 -> 20
        rc.Refit();

        var hitAfter = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hitAfter);
        Assert.Same(circle, hitAfter!.Value.Shape);
        Assert.Equal(19.0, hitAfter.Value.Point.X, 6);
    }

    [Fact]
    public void Refit_HandlesShapeMovingOutOfPath()
    {
        var circle = new VCircle(10, 0, 1);
        var rc = new RayCaster(new Shape[] { circle });

        circle.Move(new VXYZ(0, 100, 0)); // off the ray's path
        rc.Refit();

        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
    }
}
