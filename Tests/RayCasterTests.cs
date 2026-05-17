using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Code2Viz.Canvas;
using Code2Viz.Geometry;

namespace Code2Viz.Tests;

// All RayCaster tests touch the singleton CanvasRenderer.Instance.
// DisableParallelization on the collection definition prevents these
// tests from running in parallel with tests in other classes that may
// also mutate canvas state (e.g. anything constructing shapes with the
// default AutoRegister = true).
[CollectionDefinition("CanvasState", DisableParallelization = true)]
public class CanvasStateCollection { }

[Collection("CanvasState")]
public class RayCasterTests : IDisposable
{
    public RayCasterTests()
    {
        // Each test starts with auto-registration on and an empty canvas
        // so that shapes constructed inside the test become the entire
        // scene that RayCaster sees. Use the (double, ...) shape
        // constructors below — they call VPoint.Internal under the hood
        // and therefore do not auto-register extra VPoint markers.
        Shape.AutoRegister = true;
        CanvasRenderer.Instance.Clear();
    }

    public void Dispose()
    {
        CanvasRenderer.Instance.Clear();
    }

    [Fact]
    public void FindIntersection_HitsClosestOfTwoCircles()
    {
        var near = new VCircle(10, 0, 1);
        var far  = new VCircle(50, 0, 1);

        var rc = new RayCaster();
        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(near, hit!.Value.Shape);
        Assert.Equal(9.0, hit.Value.Point.X, 6);
        Assert.Equal(0.0, hit.Value.Point.Y, 6);
        Assert.Equal(9.0, hit.Value.Distance, 6);
    }

    [Fact]
    public void FindIntersection_MissesWhenRayPointsAway()
    {
        _ = new VCircle(10, 0, 1);
        var rc = new RayCaster();

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(-1, 0, 0));
        Assert.Null(hit);
    }

    [Fact]
    public void FindIntersection_HitsLineSegment()
    {
        var line = new VLine(5, -5, 5, 5);
        var rc = new RayCaster();

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(line, hit!.Value.Shape);
        Assert.Equal(5.0, hit.Value.Point.X, 6);
        Assert.Equal(0.0, hit.Value.Point.Y, 6);
        Assert.Equal(5.0, hit.Value.Distance, 6);
    }

    [Fact]
    public void FindIntersection_PrunesToCorrectShapeAmongMany()
    {
        for (int x = 0; x < 50; x++)
        for (int y = 0; y < 50; y++)
            _ = new VCircle(x * 10, y * 10, 0.4);
        // A single circle on the off-grid row (y=7) — the only shape the ray meets.
        var onlyTarget = new VCircle(300, 7, 0.4);

        var rc = new RayCaster();
        var hit = rc.FindIntersection(new VXYZ(-5, 7, 0), new VXYZ(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Same(onlyTarget, hit!.Value.Shape);
        Assert.Equal(299.6, hit.Value.Point.X, 3);
    }

    [Fact]
    public void FindIntersection_ReturnsNullForDegenerateDirection()
    {
        _ = new VCircle(0, 0, 1);
        var rc = new RayCaster();
        Assert.Null(rc.FindIntersection(new VXYZ(5, 0, 0), new VXYZ(0, 0, 0)));
    }

    [Fact]
    public void Constructor_HandlesEmptyCanvas()
    {
        var rc = new RayCaster();
        Assert.Equal(0, rc.Count);
        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
    }

    [Fact]
    public void FindIntersection_RespectsArcAngleRange()
    {
        var upper = new VArc(0, 0, 5, 0, 180);
        var rc = new RayCaster();

        var up = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(0, 1, 0));
        Assert.NotNull(up);
        Assert.Same(upper, up!.Value.Shape);
        Assert.Equal(5.0, up.Value.Point.Y, 6);

        var down = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(0, -1, 0));
        Assert.Null(down);
    }

    [Fact]
    public void FindIntersection_HitsRectangleBoundary()
    {
        // VPolygon (and therefore VRectangle) eagerly builds VLine edges in
        // BuildCurvesFromPoints() and those VLines also auto-register, so
        // the canvas snapshot indexes both the rectangle and its 4 edges.
        // Either is a geometrically correct hit at (10, 0) — verify the
        // intersection point rather than the specific Shape instance.
        _ = new VRectangle(10, -5, 10, 10);
        var rc = new RayCaster();

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Equal(10.0, hit!.Value.Point.X, 6);
        Assert.Equal(0.0, hit.Value.Point.Y, 6);
    }

    [Fact]
    public void FindIntersection_HitsEllipseAtMinorAxis()
    {
        var ellipse = new VEllipse(0, 0, 10, 3);
        var rc = new RayCaster();

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(0, 1, 0));
        Assert.NotNull(hit);
        Assert.Same(ellipse, hit!.Value.Shape);
        Assert.Equal(3.0, hit.Value.Point.Y, 6);
    }

    [Fact]
    public void FindIntersection_DoesNotAlterAutoRegisterState()
    {
        Shape.AutoRegister = true;
        _ = new VLine(5, -5, 5, 5);
        var rc = new RayCaster();

        Assert.True(Shape.AutoRegister);
        var _hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.True(Shape.AutoRegister);
    }

    // -- Canvas-aware behaviour ----------------------------------------------

    [Fact]
    public void Constructor_ExcludesHiddenShapes()
    {
        var visible = new VCircle(10, 0, 1);
        var hidden  = new VCircle(5, 0, 1);
        hidden.Hide();

        var rc = new RayCaster();
        Assert.Equal(1, rc.Count);

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Same(visible, hit!.Value.Shape);
        Assert.Equal(9.0, hit.Value.Point.X, 6);
    }

    [Fact]
    public void Constructor_SnapshotsCanvas_LaterAddsAreIgnored()
    {
        var early = new VCircle(10, 0, 1);
        var rc = new RayCaster();

        // Adding a closer circle after construction must not influence the index.
        _ = new VCircle(5, 0, 1);

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Same(early, hit!.Value.Shape);
    }

    [Fact]
    public void Constructor_IncludesAutoRegisteredVPoints()
    {
        // A bare VPoint is a valid visible canvas shape — RayCaster should
        // index it. The slab test must stay finite even when the ray is
        // axis-aligned (direction Y == 0) and the shape's AABB is degenerate
        // on that same axis (a VPoint has zero extent on both axes).
        _ = new VPoint(10, 0);
        var rc = new RayCaster();
        Assert.Equal(1, rc.Count);

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.NotNull(hit);
        Assert.Equal(10.0, hit!.Value.Point.X, 6);
        Assert.False(double.IsNaN(hit.Value.Point.Y), "Hit point Y must not be NaN");
    }

    // -- maxDistance ----------------------------------------------------------

    [Fact]
    public void FindIntersection_MaxDistance_RejectsFarHits()
    {
        _ = new VCircle(100, 0, 1);
        var rc = new RayCaster();

        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 50));
        Assert.NotNull(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 200));
    }

    [Fact]
    public void FindIntersection_MaxDistance_PicksNearestWithinRange()
    {
        var near = new VCircle(10, 0, 1);
        _ = new VCircle(100, 0, 1);
        var rc = new RayCaster();

        var hit = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 50);
        Assert.NotNull(hit);
        Assert.Same(near, hit!.Value.Shape);
    }

    // -- HasIntersection (any-hit) -------------------------------------------

    [Fact]
    public void HasIntersection_ReturnsTrueWhenAnyShapeIsHit()
    {
        _ = new VCircle(10, 0, 1);
        var rc = new RayCaster();

        Assert.True(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
        Assert.False(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(-1, 0, 0)));
    }

    [Fact]
    public void HasIntersection_RespectsMaxDistance()
    {
        _ = new VCircle(100, 0, 1);
        var rc = new RayCaster();

        Assert.False(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 50));
        Assert.True(rc.HasIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0), maxDistance: 200));
    }

    // -- Batch FindIntersections ---------------------------------------------

    [Fact]
    public void FindIntersections_BatchReturnsResultsAlignedWithInput()
    {
        var c1 = new VCircle(10, 0, 1);
        var c2 = new VCircle(0, 10, 1);
        var rc = new RayCaster();

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
        for (int i = 0; i < 200; i++)
        {
            double x = rng.NextDouble() * 100 - 50;
            double y = rng.NextDouble() * 100 - 50;
            _ = new VCircle(x, y, 0.5 + rng.NextDouble());
        }
        var rc = new RayCaster();

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
        var rc = new RayCaster();

        var hitBefore = rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0));
        Assert.Equal(9.0, hitBefore!.Value.Point.X, 6);

        // Move in place so we don't allocate a new VPoint on the canvas.
        circle.Center.X = 20;
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
        var rc = new RayCaster();

        circle.Center.Y = 100; // off the ray's path
        rc.Refit();

        Assert.Null(rc.FindIntersection(new VXYZ(0, 0, 0), new VXYZ(1, 0, 0)));
    }
}
