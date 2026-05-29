using C2VGeometry;
using Code2Viz.Animation;
using Code2Viz.Canvas;
using Xunit;

namespace Code2Viz.Tests;

[Collection("CanvasState")]
public class PathAnimationTests
{
    public PathAnimationTests()
    {
        // Reset shared canvas state — these tests mutate the singleton registry.
        Shape.DefaultRegistry = CanvasRenderer.Instance;
        CanvasRenderer.Instance.Clear();
    }

    [Fact]
    public void PathAnimation_RunsWhenPathIsHidden()
    {
        var target = new VCircle(0, 0, 5);
        var path = new VLine(0, 0, 100, 0);
        path.Hide(); // <-- the scenario the user reports

        var anim = new PathAnimation(target, path, 1.0);
        anim.Apply(0.0);          // capture initial center
        anim.Apply(1.0);          // end of path

        // Target should be translated by path's endpoint (100, 0) minus the shape's center (0, 0)
        Assert.Equal(100, target.OffsetX, precision: 6);
        Assert.Equal(0, target.OffsetY, precision: 6);
    }

    [Fact]
    public void PathAnimation_RunsWhenPathHiddenAfterConstruction()
    {
        var target = new VCircle(0, 0, 5);
        var path = new VLine(0, 0, 100, 0);
        var anim = new PathAnimation(target, path, 1.0);
        path.Hide(); // hide after constructing the animation

        anim.Apply(0.0);
        anim.Apply(0.5);

        Assert.Equal(50, target.OffsetX, precision: 6);
        Assert.Equal(0, target.OffsetY, precision: 6);
    }

}
