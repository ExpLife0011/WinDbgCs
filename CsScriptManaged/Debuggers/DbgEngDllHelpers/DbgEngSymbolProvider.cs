﻿using CsScriptManaged.SymbolProviders;
using CsScriptManaged.Utility;
using CsScripts;
using Dia2Lib;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Text;

namespace CsScriptManaged.Debuggers.DbgEngDllHelpers
{
    /// <summary>
    /// Symbol provider that is being implemented over DbgEng.dll.
    /// </summary>
    internal class DbgEngSymbolProvider : ISymbolProvider, ISymbolProviderModule
    {
        /// <summary>
        /// The DbgEngDll debugged engine
        /// </summary>
        private DbgEngDll dbgEngDll;

        /// <summary>
        /// The typed data
        /// </summary>
        private static DictionaryCache<Tuple<ulong, uint, ulong>, _DEBUG_TYPED_DATA> typedData = new DictionaryCache<Tuple<ulong, uint, ulong>, _DEBUG_TYPED_DATA>(GetTypedData);

        /// <summary>
        /// Initializes a new instance of the <see cref="DbgEngSymbolProvider"/> class.
        /// </summary>
        /// <param name="dbgEngDll">The DbgEngDll debugger engine</param>
        public DbgEngSymbolProvider(DbgEngDll dbgEngDll)
        {
            this.dbgEngDll = dbgEngDll;
        }

        /// <summary>
        /// Gets the global variable address.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="globalVariableName">Name of the global variable.</param>
        public ulong GetGlobalVariableAddress(Module module, string globalVariableName)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                string name = module.Name + "!" + globalVariableName;

                return dbgEngDll.Symbols.GetOffsetByNameWide(name);
            }
        }

        /// <summary>
        /// Gets the global variable type identifier.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="globalVariableName">Name of the global variable.</param>
        public uint GetGlobalVariableTypeId(Module module, string globalVariableName)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                string name = module.Name + "!" + globalVariableName.Replace("::", ".");
                uint typeId;
                ulong moduleId;

                dbgEngDll.Symbols.GetSymbolTypeIdWide(name, out typeId, out moduleId);
                return typeId;
            }
        }

        /// <summary>
        /// Gets the element type of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public uint GetTypeElementTypeId(Module module, uint typeId)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                var typedData = DbgEngSymbolProvider.typedData[Tuple.Create(module.Address, typeId, module.Process.PEB)];
                typedData.Data = module.Process.PEB;
                var result = dbgEngDll.Advanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, new EXT_TYPED_DATA()
                {
                    Operation = _EXT_TDOP.EXT_TDOP_GET_DEREFERENCE,
                    InData = typedData,
                }).OutData.TypeId;

                return result;
            }
        }

        /// <summary>
        /// Gets the type pointer to type of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public uint GetTypePointerToTypeId(Module module, uint typeId)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                var typedData = DbgEngSymbolProvider.typedData[Tuple.Create(module.Address, typeId, module.Process.PEB)];
                typedData.Data = module.Process.PEB;
                var result = dbgEngDll.Advanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, new EXT_TYPED_DATA()
                {
                    Operation = _EXT_TDOP.EXT_TDOP_GET_POINTER_TO,
                    InData = typedData,
                }).OutData.TypeId;

                return result;
            }
        }

        /// <summary>
        /// Gets the names of all fields of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public string[] GetTypeAllFieldNames(Module module, uint typeId)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                List<string> fields = new List<string>();
                uint nameSize;

                try
                {
                    for (uint fieldIndex = 0; ; fieldIndex++)
                    {
                        StringBuilder sb = new StringBuilder(Constants.MaxSymbolName);

                        dbgEngDll.Symbols.GetFieldName(module.Address, typeId, fieldIndex, sb, sb.Capacity, out nameSize);
                        fields.Add(sb.ToString());
                    }
                }
                catch (Exception)
                {
                }

                return fields.ToArray();
            }
        }

        /// <summary>
        /// Gets the field type id and offset of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        /// <param name="fieldName">Name of the field.</param>
        public Tuple<uint, int> GetTypeAllFieldTypeAndOffset(Module module, uint typeId, string fieldName)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                try
                {
                    uint fieldTypeId, fieldOffset;

                    dbgEngDll.Symbols.GetFieldTypeAndOffsetWide(module.Address, typeId, fieldName, out fieldTypeId, out fieldOffset);
                    return Tuple.Create(fieldTypeId, (int)fieldOffset);
                }
                catch (Exception)
                {
                    return Tuple.Create<uint, int>(0, -1);
                }
            }
        }

        /// <summary>
        /// Gets the name of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public string GetTypeName(Module module, uint typeId)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                uint nameSize;
                StringBuilder sb = new StringBuilder(Constants.MaxSymbolName);

                dbgEngDll.Symbols.GetTypeName(module.Address, typeId, sb, sb.Capacity, out nameSize);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Gets the size of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public uint GetTypeSize(Module module, uint typeId)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                return dbgEngDll.Symbols.GetTypeSize(module.Address, typeId);
            }
        }

        /// <summary>
        /// Gets the symbol tag of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public SymTag GetTypeTag(Module module, uint typeId)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                return typedData[Tuple.Create(module.Address, typeId, module.Process.PEB)].Tag;
            }
        }

        /// <summary>
        /// Gets the type identifier.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeName">Name of the type.</param>
        public uint GetTypeId(Module module, string typeName)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                return dbgEngDll.Symbols.GetTypeIdWide(module.Address, module.Name + "!" + typeName);
            }
        }

        /// <summary>
        /// Gets the source file name and line for the specified stack frame.
        /// </summary>
        /// <param name="stackFrame">The stack frame.</param>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="sourceFileLine">The source file line.</param>
        /// <param name="displacement">The displacement.</param>
        public void GetStackFrameSourceFileNameAndLine(StackFrame stackFrame, out string sourceFileName, out uint sourceFileLine, out ulong displacement)
        {
            using (StackFrameSwitcher switcher = new StackFrameSwitcher(DbgEngDll.StateCache, stackFrame))
            {
                uint fileNameLength;
                StringBuilder sb = new StringBuilder(Constants.MaxFileName);

                dbgEngDll.Symbols.GetLineByOffset(stackFrame.InstructionOffset, out sourceFileLine, sb, sb.Capacity, out fileNameLength, out displacement);
                sourceFileName = sb.ToString();
            }
        }

        /// <summary>
        /// Gets the name of the function for the specified stack frame.
        /// </summary>
        /// <param name="stackFrame">The stack frame.</param>
        /// <param name="functionName">Name of the function.</param>
        /// <param name="displacement">The displacement.</param>
        public void GetStackFrameFunctionName(StackFrame stackFrame, out string functionName, out ulong displacement)
        {
            using (StackFrameSwitcher switcher = new StackFrameSwitcher(DbgEngDll.StateCache, stackFrame))
            {
                uint functionNameSize;
                StringBuilder sb = new StringBuilder(Constants.MaxSymbolName);

                dbgEngDll.Symbols.GetNameByOffset(stackFrame.InstructionOffset, sb, sb.Capacity, out functionNameSize, out displacement);
                functionName = sb.ToString();
            }
        }

        /// <summary>
        /// Gets the source file name and line for the specified address.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="address">The address.</param>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="sourceFileLine">The source file line.</param>
        /// <param name="displacement">The displacement.</param>
        public void GetProcessAddressSourceFileNameAndLine(Process process, ulong address, out string sourceFileName, out uint sourceFileLine, out ulong displacement)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, process))
            {
                uint fileNameLength;
                StringBuilder sb = new StringBuilder(Constants.MaxFileName);

                dbgEngDll.Symbols.GetLineByOffset(address, out sourceFileLine, sb, sb.Capacity, out fileNameLength, out displacement);
                sourceFileName = sb.ToString();
            }
        }

        /// <summary>
        /// Gets the name of the function for the specified address.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="address">The address.</param>
        /// <param name="functionName">Name of the function.</param>
        /// <param name="displacement">The displacement.</param>
        public void GetProcessAddressFunctionName(Process process, ulong address, out string functionName, out ulong displacement)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, process))
            {
                uint functionNameSize;
                StringBuilder sb = new StringBuilder(Constants.MaxSymbolName);

                dbgEngDll.Symbols.GetNameByOffset(address, sb, sb.Capacity, out functionNameSize, out displacement);
                functionName = sb.ToString();
            }
        }

        /// <summary>
        /// Gets the stack frame locals.
        /// </summary>
        /// <param name="stackFrame">The stack frame.</param>
        /// <param name="arguments">if set to <c>true</c> only arguments will be returned.</param>
        public VariableCollection GetFrameLocals(StackFrame stackFrame, bool arguments)
        {
            DEBUG_SCOPE_GROUP scopeGroup = arguments ? DEBUG_SCOPE_GROUP.ARGUMENTS : DEBUG_SCOPE_GROUP.LOCALS;

            using (StackFrameSwitcher switcher = new StackFrameSwitcher(DbgEngDll.StateCache, stackFrame))
            {
                IDebugSymbolGroup2 symbolGroup;
                dbgEngDll.Symbols.GetScopeSymbolGroup2(scopeGroup, null, out symbolGroup);
                uint localsCount = symbolGroup.GetNumberSymbols();
                Variable[] variables = new Variable[localsCount];
                for (uint i = 0; i < localsCount; i++)
                {
                    StringBuilder name = new StringBuilder(Constants.MaxSymbolName);
                    uint nameSize;

                    symbolGroup.GetSymbolName(i, name, name.Capacity, out nameSize);
                    var entry = symbolGroup.GetSymbolEntryInformation(i);
                    var module = stackFrame.Process.ModulesById[entry.ModuleBase];
                    var codeType = module.TypesById[entry.TypeId];
                    var address = entry.Offset;
                    var variableName = name.ToString();

                    variables[i] = Variable.CreateNoCast(codeType, address, variableName, variableName);
                }

                return new VariableCollection(variables);
            }
        }

        /// <summary>
        /// Gets the source file name and line for the specified stack frame.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="processAddress">The process address.</param>
        /// <param name="address">The address.</param>
        /// <param name="sourceFileName">Name of the source file.</param>
        /// <param name="sourceFileLine">The source file line.</param>
        /// <param name="displacement">The displacement.</param>
        public void GetSourceFileNameAndLine(Process process, ulong processAddress, uint address, out string sourceFileName, out uint sourceFileLine, out ulong displacement)
        {
            GetProcessAddressSourceFileNameAndLine(process, processAddress, out sourceFileName, out sourceFileLine, out displacement);
        }

        /// <summary>
        /// Gets the name of the function for the specified stack frame.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="processAddress">The process address.</param>
        /// <param name="address">The address.</param>
        /// <param name="functionName">Name of the function.</param>
        /// <param name="displacement">The displacement.</param>
        public void GetFunctionNameAndDisplacement(Process process, ulong processAddress, uint address, out string functionName, out ulong displacement)
        {
            GetProcessAddressFunctionName(process, processAddress, out functionName, out displacement);
        }

        /// <summary>
        /// Gets the stack frame locals.
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="module">The module.</param>
        /// <param name="relativeAddress">The relative address.</param>
        /// <param name="arguments">if set to <c>true</c> only arguments will be returned.</param>
        public VariableCollection GetFrameLocals(StackFrame frame, Module module, uint relativeAddress, bool arguments)
        {
            return GetFrameLocals(frame, arguments);
        }

        /// <summary>
        /// Reads the simple data (1 to 8 bytes) for specified type and address to read from.
        /// </summary>
        /// <param name="codeType">Type of the code.</param>
        /// <param name="address">The address.</param>
        public ulong ReadSimpleData(CodeType codeType, ulong address)
        {
            Module module = codeType.Module;

            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                return typedData[Tuple.Create(module.Address, codeType.TypeId, address)].Data;
            }
        }

        /// <summary>
        /// Gets the names of fields of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public string[] GetTypeFieldNames(Module module, uint typeId)
        {
            throw new Exception("This is not supported using DbgEng.dll. Please use DIA symbol provider.");
        }

        /// <summary>
        /// Gets the field type id and offset of the specified type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        /// <param name="fieldName">Name of the field.</param>
        public Tuple<uint, int> GetTypeFieldTypeAndOffset(Module module, uint typeId, string fieldName)
        {
            throw new Exception("This is not supported using DbgEng.dll. Please use DIA symbol provider.");
        }

        /// <summary>
        /// Gets the type's base class type and offset.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        /// <param name="className">Name of the class.</param>
        public Tuple<uint, int> GetTypeBaseClass(Module module, uint typeId, string className)
        {
            throw new Exception("This is not supported using DbgEng.dll. Please use DIA symbol provider.");
        }

        /// <summary>
        /// Gets the name of the enumeration value.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="enumTypeId">The enumeration type identifier.</param>
        /// <param name="enumValue">The enumeration value.</param>
        public string GetEnumName(Module module, uint enumTypeId, ulong enumValue)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, module.Process))
            {
                uint enumNameSize;
                StringBuilder sb = new StringBuilder(Constants.MaxSymbolName);

                dbgEngDll.Symbols.GetConstantNameWide(module.Offset, enumTypeId, enumValue, sb, sb.Capacity, out enumNameSize);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Gets the type of the basic type.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public BasicType GetTypeBasicType(Module module, uint typeId)
        {
            // TODO: This is currently unsupported
            return BasicType.NoType;
        }

        /// <summary>
        /// Gets the type's direct base classes type and offset.
        /// </summary>
        /// <param name="module">The module.</param>
        /// <param name="typeId">The type identifier.</param>
        public Dictionary<string, Tuple<uint, int>> GetTypeDirectBaseClasses(Module module, uint typeId)
        {
            throw new Exception("This is not supported using DbgEng.dll. Please use DIA symbol provider.");
        }

        /// <summary>
        /// Gets the symbol name by address.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="address">The address.</param>
        public Tuple<string, ulong> GetSymbolNameByAddress(Process process, ulong address)
        {
            using (ProcessSwitcher switcher = new ProcessSwitcher(DbgEngDll.StateCache, process))
            {
                StringBuilder sb = new StringBuilder(Constants.MaxSymbolName);
                ulong displacement;
                uint nameSize;

                dbgEngDll.Symbols.GetNameByOffsetWide(address, sb, sb.Capacity, out nameSize, out displacement);
                return Tuple.Create(sb.ToString(), displacement);
            }
        }

        /// <summary>
        /// Gets the symbol name by address.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="address">The address.</param>
        /// <param name="distance">The distance within the module.</param>
        public Tuple<string, ulong> GetSymbolNameByAddress(Process process, ulong address, uint distance)
        {
            return GetSymbolNameByAddress(process, address);
        }

        /// <summary>
        /// Gets the typed data.
        /// </summary>
        /// <param name="typedDataId">The typed data identifier.</param>
        private static _DEBUG_TYPED_DATA GetTypedData(Tuple<ulong, uint, ulong> typedDataId)
        {
            var dbgEngDll = (DbgEngDll)Context.Debugger;

            return dbgEngDll.Advanced.Request(DEBUG_REQUEST.EXT_TYPED_DATA_ANSI, new EXT_TYPED_DATA()
            {
                Operation = _EXT_TDOP.EXT_TDOP_SET_FROM_TYPE_ID_AND_U64,
                InData = new _DEBUG_TYPED_DATA()
                {
                    ModBase = typedDataId.Item1,
                    TypeId = typedDataId.Item2,
                    Offset = typedDataId.Item3,
                },
            }).OutData;
        }
    }
}
