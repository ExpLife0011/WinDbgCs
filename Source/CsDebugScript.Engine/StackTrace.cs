﻿using CsDebugScript.Engine;
using DbgEngManaged;

namespace CsDebugScript
{
    /// <summary>
    /// Stack trace of the process being debugged.
    /// </summary>
    public class StackTrace
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StackTrace" /> class.
        /// </summary>
        /// <param name="thread">The thread.</param>
        /// <param name="frames">The frames.</param>
        /// <param name="frameContexts">The frame contexts.</param>
        internal StackTrace(Thread thread, _DEBUG_STACK_FRAME_EX[] frames, ThreadContext[] frameContexts)
        {
            Thread = thread;
            Frames = new StackFrame[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                Frames[i] = new StackFrame(this, frameContexts[i])
                {
                    Virtual = frames[i].Virtual != 0,
                    FrameNumber = frames[i].FrameNumber,
                    FrameOffset = frames[i].FrameOffset,
                    InstructionOffset = frames[i].InstructionOffset,
                    ReturnOffset = frames[i].ReturnOffset,
                    StackOffset = frames[i].StackOffset,
                };
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StackTrace"/> class.
        /// </summary>
        /// <param name="thread">The thread.</param>
        internal StackTrace(Thread thread)
        {
            Thread = thread;
        }

        /// <summary>
        /// Gets the current stack trace in current thread of current process.
        /// </summary>
        public static StackTrace Current
        {
            get
            {
                return Thread.Current.StackTrace;
            }
        }

        /// <summary>
        /// Gets the owning thread.
        /// </summary>
        public Thread Thread { get; internal set; }

        /// <summary>
        /// Gets the array of all frames.
        /// </summary>
        public StackFrame[] Frames { get; internal set; }

        /// <summary>
        /// Gets the current stack frame.
        /// </summary>
        public StackFrame CurrentFrame
        {
            get
            {
                return Context.Debugger.GetThreadCurrentStackFrame(Thread);
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"Frames = {Frames.Length}";
        }
    }
}
