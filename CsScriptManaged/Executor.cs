﻿using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace CsScriptManaged
{
    /// <summary>
    /// Exported class for transferring execution from native to managed code
    /// </summary>
    [Guid("E2C73928-FEA4-4DF6-AD34-82BAAFC024B8"), ComVisible(true)]
    internal sealed class Executor : IExecutor
    {
        /// <summary>
        /// Initializes the context.
        /// </summary>
        /// <param name="client">The client.</param>
        public void InitializeContext(IDebugClient client)
        {
            Context.Initalize(client);
        }

        /// <summary>
        /// Executes the script.
        /// </summary>
        /// <param name="scriptPath">The script path.</param>
        /// <param name="args">The arguments.</param>
        public void ExecuteScript(string scriptPath, string[] args)
        {
            Context.Execute(scriptPath, args);
        }

        /// <summary>
        /// Executes the script.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        public void ExecuteScript(string arguments)
        {
            string[] args = arguments.Split(" ".ToCharArray());

            ExecuteScript(args[0], args.Skip(1).ToArray());
        }

        /// <summary>
        /// Enters the interactive mode.
        /// </summary>
        /// <param name="arguments">The arguments</param>
        public void EnterInteractiveMode(string arguments)
        {
            Context.EnterInteractiveMode();
        }

        /// <summary>
        /// Interprets C# code.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        public void Interpret(string arguments)
        {
            Context.Interpret(arguments);
        }

        /// <summary>
        /// Opens the UI.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        public void OpenUI(string arguments)
        {
            string[] args = arguments.Split(" ".ToCharArray());
            bool showModal = false;

            if (args.Length > 0 && !bool.TryParse(args[0], out showModal))
            {
                showModal = false;
            }

            Context.ShowInteractiveWindow(showModal);
        }
    }
}
