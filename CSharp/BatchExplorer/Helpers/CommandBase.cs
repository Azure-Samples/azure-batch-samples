//Copyright (c) Microsoft Corporation

using System;
using System.Windows.Input;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    /// <summary>
    /// The basic implementation for ICommand - use directly with command binding or as a base for more advanced command types
    /// </summary>
    public class CommandBase : ICommand
    {
        /// <summary>
        /// Create a command that is ALWAYS ready to be invoked...ALWAYS
        /// </summary>
        /// <param name="execute">the delegate to execute when the command is invoked</param>
        public CommandBase(Action<object> execute)
            :this(execute,null)
        {
            
        }
        /// <summary>
        /// Create a command that needs a test method to see if it can be executed
        /// </summary>
        /// <param name="execute">The delegate to execute when the command is invoked</param>
        /// <param name="canExecute">The method to test whether this command CAN be invoked</param>
        public CommandBase(Action<object> execute, Func<object, bool> canExecute)
        {
            this.execute = execute;
            if (canExecute != null)
            {
                this.canExecute = canExecute;
            }
            else
            {
                //if the canExecute was null, replace it with an anonymous delegate that always returns true
                this.canExecute = (o) => { return true; };
            }
        }
        /// <summary>
        /// The method that evaluates whether this command can be invoked
        /// </summary>
        /// <param name="parameter">the parameter for this evaluation</param>
        /// <returns>true if the command can be invoked</returns>
        public bool CanExecute(object parameter)
        {
            return this.canExecute(parameter);
        }
        /// <summary>
        /// The method executed when this command is invoked
        /// </summary>
        /// <param name="parameter">the parameter for this command</param>
        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
#pragma warning disable 67  // we're required to declare this for ICommand interface, but we don't use it in any of our command classes
        public event EventHandler CanExecuteChanged;
#pragma warning restore

        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;
    }
}
