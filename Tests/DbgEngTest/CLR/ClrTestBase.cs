﻿using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DbgEngTest.CLR
{
    public class ClrTestBase : TestBase
    {
        protected static string CompileApp(string appName, params string[] files)
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string destination = Path.Combine(directory, appName + ".exe");
            string pdbPath = Path.Combine(directory, appName + ".pdb");
            string[] fullPathFiles = files.Select(f => Path.Combine(directory, "CLR", "Apps", f)).ToArray();

            // Check if we need to compile at all
            if (File.Exists(destination) && File.Exists(pdbPath))
            {
                bool upToDate = true;

                foreach (var file in fullPathFiles)
                {
                    if (File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(destination))
                    {
                        upToDate = false;
                        break;
                    }
                }

                if (upToDate)
                {
                    return destination;
                }
            }

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters cp = new CompilerParameters();

            cp.ReferencedAssemblies.Add("system.dll");
            cp.GenerateInMemory = false;
            cp.GenerateExecutable = true;
            cp.CompilerOptions = IntPtr.Size == 4 ? "/platform:x86" : "/platform:x64";
            cp.IncludeDebugInformation = true;
            cp.OutputAssembly = destination;
            CompilerResults cr = provider.CompileAssemblyFromFile(cp, fullPathFiles);

            if (cr.Errors.Count > 0 && System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
            }

            Assert.AreEqual(0, cr.Errors.Count);

            return cr.PathToAssembly;
        }

        protected static void CompileAndInitialize(string appName)
        {
            string appPath = CompileApp(appName, appName + ".cs", "SharedLibrary.cs");
            string appDirectory = Path.GetDirectoryName(appPath);
            string dumpPath = Path.Combine(appDirectory, Path.GetFileNameWithoutExtension(appName) + ".mdmp");

            if (!File.Exists(dumpPath) || File.GetLastWriteTimeUtc(appPath) > File.GetLastWriteTimeUtc(dumpPath))
            {
                ExceptionDumper.Dumper.RunAndDumpOnException(appPath, dumpPath, false);
            }

            Initialize(dumpPath, appDirectory);
        }
    }
}
