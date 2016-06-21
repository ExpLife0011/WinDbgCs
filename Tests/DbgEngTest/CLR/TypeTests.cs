﻿using CsDebugScript;
using CsDebugScript.CLR;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbgEngTest.CLR
{
    [TestClass]
    public class TypeTests : ClrTestBase
    {
        [ClassInitialize]
        public static void TestSetup(TestContext context)
        {
            SyncStart();
            CompileAndInitialize(ClrTestApps.Types);
        }

        [ClassCleanup]
        public static void TestCleanup()
        {
            SyncStop();
        }

        [TestMethod]
        public void IntegerObjectClrType()
        {
            Heap heap = Process.Current.ClrRuntimes.Single().Heap;
            Module typesModule = Module.All.Single(m => m.Name == "Types");
            Variable s_i = typesModule.GetVariable("Types.s_i");

            Assert.AreEqual(42, (int)s_i);
            Assert.IsTrue(heap.EnumerateObjects().Contains(s_i));
        }

        [TestMethod]
        public void FieldNameAndValueTests()
        {
            Heap heap = Process.Current.ClrRuntimes.Single().Heap;
            Module typesModule = Module.All.Single(m => m.Name == "Types");
            CodeType fooType = CodeType.Create("Foo", typesModule);
            Variable s_foo = typesModule.GetVariable("Types.s_foo");

            Assert.AreEqual(fooType, s_foo.GetCodeType());
            Assert.AreEqual(42, (int)s_foo.GetField("i"));
            Assert.AreEqual("string", new ClrString(s_foo.GetField("s")).Text);
            Assert.AreEqual(true, (bool)s_foo.GetField("b"));
            Assert.AreEqual(4.2f, (float)s_foo.GetField("f"));
            Assert.AreEqual(8.4, (double)s_foo.GetField("d"));
            Assert.IsTrue(heap.EnumerateObjects().Contains(s_foo));
        }

        [TestMethod]
        public void ArrayTests()
        {
            Module typesModule = Module.All.Single(m => m.Name == "Types");
            CodeType fooType = CodeType.Create("Types", typesModule);
            Variable s_array = fooType.GetStaticField("s_array");
            Variable s_one = fooType.GetStaticField("s_one");
            Variable s_two = fooType.GetStaticField("s_two");
            Variable s_three = fooType.GetStaticField("s_three");

            CodeArray<Variable> codeArray = new CodeArray<Variable>(s_array);
            Variable[] expected = new Variable[] { s_one, s_two, s_three };

            Assert.AreEqual(expected.Length, codeArray.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], codeArray[i]);
            }
        }
    }
}
