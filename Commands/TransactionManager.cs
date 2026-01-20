using System;
using System.Collections.Generic;

namespace Code2Viz.Commands
{
    /// <summary>
    /// Manages undo/redo operations using the Command Pattern.
    /// Provides transaction support for grouping multiple commands as a single undo step.
    /// Similar to Revit API's Transaction model.
    /// </summary>
    public class TransactionManager
    {
        private static TransactionManager? _instance;
        private static readonly object _lock = new();

        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();
        private readonly List<ICommand> _transactionCommands = new();
        private string? _transactionName;
        private bool _isInTransaction;

        /// <summary>
        /// Maximum number of commands to keep in the undo stack.
        /// </summary>
        public int MaxUndoLevels { get; set; } = 100;

        /// <summary>
        /// Whether an undo operation is available.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Whether a redo operation is available.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Description of the next command to undo, or null if none.
        /// </summary>
        public string? UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;

        /// <summary>
        /// Description of the next command to redo, or null if none.
        /// </summary>
        public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Number of commands in the undo stack.
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Number of commands in the redo stack.
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Whether a transaction is currently active.
        /// </summary>
        public bool IsInTransaction => _isInTransaction;

        /// <summary>
        /// Event raised when the undo/redo state changes.
        /// </summary>
        public event EventHandler? StateChanged;

        /// <summary>
        /// Singleton instance of the TransactionManager.
        /// </summary>
        public static TransactionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new TransactionManager();
                    }
                }
                return _instance;
            }
        }

        private TransactionManager() { }

        /// <summary>
        /// Executes a command and adds it to the undo stack.
        /// If a transaction is active, the command is added to the transaction instead.
        /// </summary>
        public void Execute(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            command.Execute();

            if (_isInTransaction)
            {
                _transactionCommands.Add(command);
            }
            else
            {
                AddToUndoStack(command);
            }
        }

        /// <summary>
        /// Adds a command to the undo stack without executing it.
        /// Use when the command has already been executed externally.
        /// </summary>
        public void RecordCommand(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            if (_isInTransaction)
            {
                _transactionCommands.Add(command);
            }
            else
            {
                AddToUndoStack(command);
            }
        }

        private void AddToUndoStack(ICommand command)
        {
            // Try to merge with the last command if possible
            if (_undoStack.Count > 0)
            {
                var lastCommand = _undoStack.Peek();
                if (lastCommand.CanMergeWith(command))
                {
                    lastCommand.MergeWith(command);
                    _redoStack.Clear();
                    OnStateChanged();
                    return;
                }
            }

            _undoStack.Push(command);
            _redoStack.Clear();

            // Enforce max undo levels
            TrimUndoStack();

            OnStateChanged();
        }

        private void TrimUndoStack()
        {
            if (_undoStack.Count > MaxUndoLevels)
            {
                // Convert to array, keep only the newest MaxUndoLevels
                var commands = _undoStack.ToArray();
                _undoStack.Clear();
                for (int i = MaxUndoLevels - 1; i >= 0; i--)
                {
                    _undoStack.Push(commands[i]);
                }
            }
        }

        /// <summary>
        /// Undoes the last command.
        /// </summary>
        /// <returns>True if a command was undone, false if the undo stack was empty.</returns>
        public bool Undo()
        {
            if (_undoStack.Count == 0) return false;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            OnStateChanged();
            return true;
        }

        /// <summary>
        /// Redoes the last undone command.
        /// </summary>
        /// <returns>True if a command was redone, false if the redo stack was empty.</returns>
        public bool Redo()
        {
            if (_redoStack.Count == 0) return false;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);

            OnStateChanged();
            return true;
        }

        /// <summary>
        /// Begins a transaction. All commands executed until CommitTransaction() are grouped
        /// as a single undo step.
        /// </summary>
        /// <param name="name">Description for the transaction (shown in undo menu).</param>
        public void BeginTransaction(string name)
        {
            if (_isInTransaction)
            {
                throw new InvalidOperationException("A transaction is already in progress. Commit or rollback first.");
            }

            _isInTransaction = true;
            _transactionName = name;
            _transactionCommands.Clear();
        }

        /// <summary>
        /// Commits the current transaction, grouping all executed commands as one undo step.
        /// </summary>
        public void CommitTransaction()
        {
            if (!_isInTransaction)
            {
                throw new InvalidOperationException("No transaction is in progress.");
            }

            _isInTransaction = false;

            if (_transactionCommands.Count > 0)
            {
                if (_transactionCommands.Count == 1)
                {
                    // Single command - add directly
                    AddToUndoStack(_transactionCommands[0]);
                }
                else
                {
                    // Multiple commands - wrap in composite
                    var composite = new CompositeCommand(_transactionName ?? "Transaction", _transactionCommands.ToArray());
                    AddToUndoStack(composite);
                }
            }

            _transactionCommands.Clear();
            _transactionName = null;
        }

        /// <summary>
        /// Rolls back the current transaction, undoing all commands executed within it.
        /// </summary>
        public void RollbackTransaction()
        {
            if (!_isInTransaction)
            {
                throw new InvalidOperationException("No transaction is in progress.");
            }

            _isInTransaction = false;

            // Undo commands in reverse order
            for (int i = _transactionCommands.Count - 1; i >= 0; i--)
            {
                _transactionCommands[i].Undo();
            }

            _transactionCommands.Clear();
            _transactionName = null;

            OnStateChanged();
        }

        /// <summary>
        /// Clears both undo and redo stacks.
        /// Call when loading a new document or clearing the canvas.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _transactionCommands.Clear();
            _isInTransaction = false;
            _transactionName = null;

            OnStateChanged();
        }

        /// <summary>
        /// Gets descriptions of all commands in the undo stack (most recent first).
        /// </summary>
        public IEnumerable<string> GetUndoHistory()
        {
            foreach (var command in _undoStack)
            {
                yield return command.Description;
            }
        }

        /// <summary>
        /// Gets descriptions of all commands in the redo stack (most recent first).
        /// </summary>
        public IEnumerable<string> GetRedoHistory()
        {
            foreach (var command in _redoStack)
            {
                yield return command.Description;
            }
        }

        protected virtual void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
