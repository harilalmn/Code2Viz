using System.Collections.Generic;
using System.Linq;

namespace Code2Viz.Commands
{
    /// <summary>
    /// A command that groups multiple commands as a single undo/redo step.
    /// Used for transactions and complex operations that involve multiple changes.
    /// </summary>
    public class CompositeCommand : ICommand
    {
        private readonly List<ICommand> _commands;

        public string Description { get; }

        /// <summary>
        /// Gets the number of commands in this composite.
        /// </summary>
        public int Count => _commands.Count;

        /// <summary>
        /// Gets the child commands.
        /// </summary>
        public IReadOnlyList<ICommand> Commands => _commands.AsReadOnly();

        /// <summary>
        /// Creates a composite command from an array of commands.
        /// </summary>
        /// <param name="description">Description for the composite operation.</param>
        /// <param name="commands">Commands to group together.</param>
        public CompositeCommand(string description, params ICommand[] commands)
        {
            Description = description;
            _commands = commands.ToList();
        }

        /// <summary>
        /// Creates a composite command from a collection of commands.
        /// </summary>
        /// <param name="description">Description for the composite operation.</param>
        /// <param name="commands">Commands to group together.</param>
        public CompositeCommand(string description, IEnumerable<ICommand> commands)
        {
            Description = description;
            _commands = commands.ToList();
        }

        /// <summary>
        /// Adds a command to this composite.
        /// </summary>
        public void Add(ICommand command)
        {
            _commands.Add(command);
        }

        /// <summary>
        /// Executes all commands in order.
        /// </summary>
        public void Execute()
        {
            foreach (var command in _commands)
            {
                command.Execute();
            }
        }

        /// <summary>
        /// Undoes all commands in reverse order.
        /// </summary>
        public void Undo()
        {
            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                _commands[i].Undo();
            }
        }

        /// <summary>
        /// Composite commands don't merge with other commands.
        /// </summary>
        public bool CanMergeWith(ICommand other) => false;

        /// <summary>
        /// Not supported for composite commands.
        /// </summary>
        public void MergeWith(ICommand other)
        {
            // Not supported
        }
    }
}
