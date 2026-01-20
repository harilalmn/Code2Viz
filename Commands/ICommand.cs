namespace Code2Viz.Commands
{
    /// <summary>
    /// Interface for undoable commands following the Command Pattern.
    /// All operations that modify the canvas or document state should implement this interface.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Human-readable description of the command for UI display.
        /// Example: "Draw Circle", "Move 3 shapes", "Delete Line"
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Executes the command. Called when the command is first performed or when redoing.
        /// </summary>
        void Execute();

        /// <summary>
        /// Reverses the command's effects. Called when undoing.
        /// </summary>
        void Undo();

        /// <summary>
        /// Whether this command can be merged with a subsequent command of the same type.
        /// Used for continuous operations like dragging where many small moves should be one undo step.
        /// </summary>
        bool CanMergeWith(ICommand other);

        /// <summary>
        /// Merges another command into this one. Only called if CanMergeWith returns true.
        /// </summary>
        void MergeWith(ICommand other);
    }
}
