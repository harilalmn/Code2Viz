using Code2Viz.Canvas;
using Code2Viz.Geometry;

namespace Code2Viz.Services
{
    /// <summary>
    /// Interface for canvas rendering services.
    /// Provides shape collection management and rendering to a canvas.
    /// </summary>
    public interface ICanvasRenderer
    {
        /// <summary>
        /// Adds a shape to the render collection.
        /// Shapes with IsPlaced = true are skipped to prevent duplicates.
        /// </summary>
        void AddShape(IDrawable shape);

        /// <summary>
        /// Gets a read-only list of all shapes in the collection.
        /// </summary>
        IReadOnlyList<IDrawable> GetShapes();

        /// <summary>
        /// Clears all shapes and resets their IsPlaced flags.
        /// Also stops and clears the active timeline.
        /// </summary>
        void Clear();

        /// <summary>
        /// Renders all shapes to the specified canvas.
        /// Optionally performs zoom-to-fit based on application settings.
        /// </summary>
        void RenderTo(RenderCanvas canvas);
    }
}
