﻿using CsScriptManaged;
using CsScriptManaged.Utility;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection.Emit;

namespace CsScripts
{
    /// <summary>
    /// Ultimate class for working with variables from process being debugged.
    /// </summary>
    public class Variable : DynamicObject, IConvertible
    {
        /// <summary>
        /// The name of variable when its value is computed
        /// </summary>
        public const string ComputedName = "<computed>";

        /// <summary>
        /// The unknown path
        /// </summary>
        public const string UnknownPath = "<unknown>";

        /// <summary>
        /// The untracked path
        /// </summary>
        public const string UntrackedPath = "<untracked>";

        /// <summary>
        /// The name
        /// </summary>
        private string name;

        /// <summary>
        /// The path
        /// </summary>
        private string path;

        /// <summary>
        /// The code type
        /// </summary>
        private CodeType codeType;

        /// <summary>
        /// Runtime code type.
        /// </summary>
        private SimpleCacheStruct<CodeType> runtimeCodeType;

        /// <summary>
        /// The data
        /// </summary>
        private SimpleCacheStruct<ulong> data;

        /// <summary>
        /// Initializes a new instance of the <see cref="Variable"/> class.
        /// </summary>
        /// <param name="variable">The variable.</param>
        public Variable(Variable variable)
        {
            name = variable.name;
            path = variable.path;
            Address = variable.Address;
            codeType = variable.codeType;
            data = variable.data;
            runtimeCodeType = variable.runtimeCodeType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Variable" /> class.
        /// </summary>
        /// <param name="codeType">The code type.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        internal Variable(CodeType codeType, ulong address, string name, string path)
        {
            this.codeType = codeType;
            this.name = name;
            this.path = path;
            Address = address;

            // Initialize caches
            data = SimpleCache.CreateStruct(ReadData);
            runtimeCodeType = SimpleCache.CreateStruct(FindRuntimeCodeType);
        }

        /// <summary>
        /// Reuses this object with new values
        /// </summary>
        /// <param name="codeType">Type of the code.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        internal protected void ReuseUserType(CodeType codeType, ulong address, string name, string path)
        {
            this.codeType = codeType;
            this.name = name;
            this.path = path;
            Address = address;

            // Initialize caches
            data = SimpleCache.CreateStruct(ReadData);
            runtimeCodeType = SimpleCache.CreateStruct(FindRuntimeCodeType);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Variable" /> class.
        /// </summary>
        /// <param name="codeType">The code type.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        /// <param name="data">The loaded data value (this can be used only with pointers).</param>
        private Variable(CodeType codeType, ulong address, string name, string path, ulong data)
            : this(codeType, address, name, path)
        {
            if (!codeType.IsPointer)
            {
                throw new Exception("You cannot assign data to non-pointer type variable. Type was " + codeType);
            }

            this.data.Value = data;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Variable" /> class.
        /// </summary>
        /// <param name="codeType">The code type.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        public static Variable Create(CodeType codeType, ulong address, string name = ComputedName, string path = UnknownPath)
        {
            Variable variable = CreateNoCast(codeType, address, name, path);

            return codeType.Module.Process.CastVariableToUserType(variable);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Variable" /> class and doesn't cast it to user code type.
        /// </summary>
        /// <param name="codeType">The code type.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        public static Variable CreateNoCast(CodeType codeType, ulong address, string name = ComputedName, string path = UnknownPath)
        {
            if (Context.EnableVariableCaching)
            {
                return codeType.Module.Process.Variables[Tuple.Create(codeType, address, name, path)];
            }

            return new Variable(codeType, address, name, path);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Variable" /> class of pointer type.
        /// </summary>
        /// <param name="codeType">The code type.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        public static Variable CreatePointer(CodeType codeType, ulong address, string name = ComputedName, string path = UnknownPath)
        {
            Variable variable = CreatePointerNoCast(codeType, address, name, path);

            return codeType.Module.Process.CastVariableToUserType(variable);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Variable" /> class of pointer type and doesn't cast it to user code type.
        /// </summary>
        /// <param name="codeType">The code type.</param>
        /// <param name="address">The address.</param>
        /// <param name="name">The name.</param>
        /// <param name="path">The path.</param>
        public static Variable CreatePointerNoCast(CodeType codeType, ulong address, string name = ComputedName, string path = UnknownPath)
        {
            return new Variable(codeType, 0, name, path, address);
        }

        /// <summary>
        /// The address where this variable value is stored
        /// </summary>
        internal ulong Address { get; private set; }

        /// <summary>
        /// Gets the loaded data value.
        /// </summary>
        internal ulong Data
        {
            get
            {
                return data.Value;
            }
        }

        #region Simple casts
        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Boolean"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator bool (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsPointer && !v.codeType.IsEnum)
            {
                return bool.Parse(v.ToString());
            }

            return v.Data != 0;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Byte"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator byte (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return byte.Parse(v.ToString());
            }

            return (byte)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.SByte"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator sbyte (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return sbyte.Parse(v.ToString());
            }

            return (sbyte)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Char"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator char (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return char.Parse(v.ToString());
            }

            return (char)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Int16"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator short (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return short.Parse(v.ToString());
            }

            return (short)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.UInt16"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator ushort (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return ushort.Parse(v.ToString());
            }

            return (ushort)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Int32"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator int (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return int.Parse(v.ToString());
            }

            return (int)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.UInt32"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator uint (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return uint.Parse(v.ToString());
            }

            return (uint)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Int64"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator long (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return long.Parse(v.ToString());
            }

            return (long)v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.UInt64"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator ulong (Variable v)
        {
            if (!v.codeType.IsSimple && !v.codeType.IsEnum)
            {
                return ulong.Parse(v.ToString());
            }

            return v.Data;
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Single"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator float (Variable v)
        {
            if (v.codeType.IsDouble)
            {
                return (float)(double)v;
            }

            if (v.codeType.IsFloat)
            {
                return BitConverter.ToSingle(BitConverter.GetBytes((uint)v.Data), 0);
            }

            return float.Parse(v.ToString());
        }

        /// <summary>
        /// Performs an explicit conversion from <see cref="Variable"/> to <see cref="System.Double"/>.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static explicit operator double (Variable v)
        {
            if (v.codeType.IsDouble)
            {
                return BitConverter.Int64BitsToDouble((long)v.Data);
            }

            if (v.codeType.IsFloat)
            {
                return (float)v;
            }

            return double.Parse(v.ToString());
        }
        #endregion

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        /// <exception cref="System.ArgumentException">Incorrect data size</exception>
        public override string ToString()
        {
            // Check if it is null
            if (IsNullPointer())
            {
                return "(null)";
            }

            // ANSI string
            if (codeType.IsAnsiString)
            {
                return UserType.ReadString(codeType.Module.Process, GetPointerAddress(), 1);
            }

            // Unicode string
            if (codeType.IsWideString)
            {
                return UserType.ReadString(codeType.Module.Process, GetPointerAddress(), 2);
            }

            // Check float/double
            if (codeType.IsFloat)
            {
                return ((float)this).ToString();
            }

            if (codeType.IsDouble)
            {
                return ((double)this).ToString();
            }

            // Simple type
            if (codeType.IsSimple)
            {
                if (codeType.Name == "bool" || codeType.Name == "BOOL")
                {
                    return (Data != 0).ToString();
                }

                switch (codeType.Size)
                {
                    case 1:
                        return ((byte)Data).ToString();
                    case 2:
                        return ((short)Data).ToString();
                    case 4:
                        return ((int)Data).ToString();
                    case 8:
                        return ((long)Data).ToString();
                    default:
                        throw new ArgumentException("Incorrect data size " + codeType.Size);
                }
            }

            // Enumeration
            if (codeType.IsEnum)
            {
                return Context.SymbolProvider.GetEnumName(codeType.Module, codeType.TypeId, Data);
            }

            // TODO: Call custom caster (e.g. std::string, std::wstring)

            // Check if it is pointer
            if (codeType.IsPointer)
            {
                if (codeType.Size == 4)
                {
                    return string.Format("0x{0:X4}", Data);
                }
                else
                {
                    return string.Format("0x{0:X8}", Data);
                }
            }

            return "{" + codeType.Name + "}";
        }

        /// <summary>
        /// Determines whether this variable is null pointer.
        /// </summary>
        /// <returns></returns>
        public bool IsNullPointer()
        {
            return codeType.IsPointer && GetPointerAddress() == 0;
        }

        /// <summary>
        /// Gets the pointer address.
        /// </summary>
        /// <exception cref="System.ArgumentException">Variable is not a pointer type, but ...</exception>
        public ulong GetPointerAddress()
        {
            return codeType.IsPointer ? Data : Address;
        }

        /// <summary>
        /// Gets the memory address where value of this Variable is stored.
        /// </summary>
        public ulong GetAddress()
        {
            return Address;
        }

        /// <summary>
        /// Gets the name of variable.
        /// </summary>
        public string GetName()
        {
            return name;
        }

        /// <summary>
        /// Gets the path of variable.
        /// </summary>
        public string GetPath()
        {
            return path;
        }

        /// <summary>
        /// Gets the code type.
        /// </summary>
        public CodeType GetCodeType()
        {
            return codeType;
        }

        /// <summary>
        /// Gets the runtime type.
        /// </summary>
        public CodeType GetRuntimeType()
        {
            return runtimeCodeType.Value;
        }

        /// <summary>
        /// Finds the runtime code type by looking at the v-table.
        /// </summary>
        private CodeType FindRuntimeCodeType()
        {
            // TODO: See if it is complex type and try to get VTable
            try
            {
                if (!codeType.IsSimple || codeType.IsPointer)
                {
                    CodeType ulongType = CodeType.Create("unsigned long long", codeType.Module);
                    ulong vtableAddress = Context.SymbolProvider.ReadSimpleData(ulongType, GetPointerAddress());
                    string vtableName = Context.SymbolProvider.GetSymbolNameByAddress(codeType.Module.Process, vtableAddress).Item1;

                    if (vtableName.EndsWith("::`vftable'"))
                    {
                        vtableName = vtableName.Substring(0, vtableName.Length - 11);
                        if (vtableName.StartsWith("const "))
                        {
                            vtableName = vtableName.Substring(6);
                        }

                        return vtableName.IndexOf('!') > 0 ? CodeType.Create(vtableName) : CodeType.Create(vtableName, codeType.Module);
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to original code type
            }

            return codeType;
        }

        /// <summary>
        /// Gets the field names (including base classes).
        /// </summary>
        public string[] GetFieldNames()
        {
            return codeType.FieldNames;
        }

        /// <summary>
        /// Gets the field names (it doesn't include base classes).
        /// </summary>
        public string[] GetClassFieldNames()
        {
            return codeType.ClassFieldNames;
        }

        /// <summary>
        /// Gets the fields (including base classes).
        /// </summary>
        public Variable[] GetFields()
        {
            return FindUserTypeCastedFields();
        }

        /// <summary>
        /// Gets the not user type casted fields (original ones).
        /// </summary>
        internal Variable[] GetOriginalFields()
        {
            return FindFields();
        }

        /// <summary>
        /// Gets the array element.
        /// </summary>
        /// <param name="index">The index.</param>
        public Variable GetArrayElement(int index)
        {
            if (!codeType.IsArray && !codeType.IsPointer)
            {
                throw new ArgumentException("Variable is not a array or pointer type, but " + codeType);
            }

            CodeType elementType = codeType.ElementType;
            ulong baseAddress = GetPointerAddress();
            ulong address = baseAddress + (ulong)(index * elementType.Size);

            return Create(elementType, address, ComputedName, GenerateNewName("[{0}]", index));
        }

        /// <summary>
        /// Dereferences the pointer.
        /// </summary>
        /// <exception cref="System.ArgumentException">Variable is not a pointer type, but ...</exception>
        public Variable DereferencePointer()
        {
            if (!codeType.IsPointer)
            {
                throw new ArgumentException("Variable is not a pointer type, but " + codeType);
            }

            return GetArrayElement(0);
        }

        /// <summary>
        /// Adjusts the pointer.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <remarks>Returned variable is not casted to the user type. Use CastAs function to cast to suitable type.</remarks>
        /// <returns>Computed variable that points to new address</returns>
        /// <exception cref="System.ArgumentException">Variable is not a pointer type, but ...</exception>
        public Variable AdjustPointer(int offset)
        {
            if (codeType.IsPointer)
            {
                return CreatePointerNoCast(codeType, GetPointerAddress() + (ulong)offset, name, path);
            }
            else if (Address != 0)
            {
                return CreateNoCast(codeType, Address + (ulong)offset, name, path);
            }

            throw new ArgumentException("Variable is not a pointer type, but " + codeType + " and its address is 0");
        }

        /// <summary>
        /// Casts variable to new type.
        /// </summary>
        /// <param name="newType">The type.</param>
        /// <returns>Computed variable that is of new type.</returns>
        public Variable CastAs(CodeType newType)
        {
            Variable variable;

            if (newType == codeType)
            {
                return this;
            }
            else if (codeType.IsPointer && newType.IsPointer)
            {
                variable = new Variable(newType, Address, name, path, Data);
            }
            else if (newType.IsPointer)
            {
                variable = CreatePointerNoCast(newType, Address, name, path);
            }
            else if (codeType.IsPointer)
            {
                variable = CreateNoCast(newType, Data, name, path);
            }
            else if (Address != 0)
            {
                return Create(newType, Address, name, path);
            }
            else
            {
                throw new ArgumentException("Variable is not a pointer type, but " + codeType + " and its address is 0");
            }

            return newType.Module.Process.CastVariableToUserType(variable);
        }

        /// <summary>
        /// Casts the specified variable to the new type.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <returns>Computed variable that is of new type.</returns>
        public static T CastAs<T>(Variable variable)
        {
            if (variable == null)
            {
                return default(T);
            }

            return variable.CastAs<T>();
        }

        /// <summary>
        /// Casts variable to the new type.
        /// </summary>
        /// <returns>Computed variable that is of new type.</returns>
        public T CastAs<T>()
        {
            return (T)CastAs(typeof(T));
        }

        /// <summary>
        /// Casts variable to the new type.
        /// </summary>
        /// <param name="conversionType">The new type.</param>
        public object CastAs(Type conversionType)
        {
            // If we are converting Variable to Variable, just return us
            if (conversionType == typeof(Variable) || conversionType == GetType())
                return this;

            // Check if it is basic type
            else if (conversionType.IsPrimitive)
            {
                if (conversionType == typeof(bool))
                    return (bool)this;
                else if (conversionType == typeof(char))
                    return (char)this;
                else if (conversionType == typeof(byte))
                    return (byte)this;
                else if (conversionType == typeof(sbyte))
                    return (sbyte)this;
                else if (conversionType == typeof(short))
                    return (short)this;
                else if (conversionType == typeof(ushort))
                    return (ushort)this;
                else if (conversionType == typeof(int))
                    return (int)this;
                else if (conversionType == typeof(uint))
                    return (uint)this;
                else if (conversionType == typeof(long))
                    return (long)this;
                else if (conversionType == typeof(ulong))
                    return (ulong)this;
            }

            // Check if it is null
            if (GetPointerAddress() == 0)
            {
                return null;
            }

            Variable activatorParameter = this;

            // Check if we should do CastAs
            if (conversionType.IsSubclassOf(typeof(Variable)))
            {
                var description = codeType.Module.Process.TypeToUserTypeDescription[conversionType].FromModuleOrFirst(codeType.Module);
                CodeType newType = description.UserType;

                // Check if it was non-unique generics type
                if (newType != null)
                {
                    // If type is already in the metadata cache, use it
                    if (newType.Module.Process.UserTypes.Contains(description))
                    {
                        return CastAs(newType);
                    }

                    if (codeType.IsPointer && !newType.IsPointer)
                    {
                        newType = newType.PointerToType;
                    }

                    activatorParameter = CastAs(newType);
                }
            }

            // Use symbolic constructor to convert variable to user type
            var activator = GlobalCache.UserTypeDelegates[conversionType].SymbolicConstructor;

            if (activator == null)
            {
                throw new InvalidCastException("Cannot cast Variable to " + conversionType);
            }

            return activator(activatorParameter);
        }

        /// <summary>
        /// Casts variable to new type.
        /// </summary>
        /// <param name="newType">The new type.</param>
        /// <returns>Computed variable that is of new type.</returns>
        public Variable CastAs(string newType)
        {
            CodeType newCodeType;

            if (newType.IndexOf('!') > 0)
            {
                newCodeType = CodeType.Create(newType);
            }
            else
            {
                newCodeType = CodeType.Create(newType, codeType.Module);
            }

            return CastAs(newCodeType);
        }

        /// <summary>
        /// Gets the variable that is casted to base class given by name.
        /// This is mostly used by auto generated code (exported from PDB) or to access multi inheritance base classes.
        /// </summary>
        /// <remarks>This is not casted to user type</remarks>
        /// <param name="className">The class name.</param>
        public Variable GetBaseClass(string className)
        {
            if (codeType.Name == className)
            {
                return this;
            }

            var tuple = codeType.BaseClasses[className];
            var newCodeType = tuple.Item1;

            if (newCodeType == codeType)
            {
                return this;
            }

            if (!newCodeType.IsPointer && codeType.IsPointer)
            {
                newCodeType = newCodeType.PointerToType;
            }

            if (newCodeType == codeType)
            {
                return this;
            }

            if (newCodeType.IsPointer)
            {
                return CreatePointerNoCast(newCodeType, GetPointerAddress() + (uint)tuple.Item2, name, path);
            }
            else
            {
                return CreateNoCast(newCodeType, GetPointerAddress() + (uint)tuple.Item2, name, path);
            }
        }

        /// <summary>
        /// GetBaseClass with cast.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="className"></param>
        /// <returns></returns>
        public T GetBaseClass<T>(string className)
        {
            return GetBaseClass(className).CastAs<T>();
        }

        /// <summary>
        /// Gets the class field (it doesn't go through base classes).
        /// </summary>
        /// <param name="fieldName">Name of the field.</param>
        public Variable GetClassField(string fieldName)
        {
            var tuple = codeType.ClassFields[fieldName];
            CodeType fieldType = tuple.Item1;
            ulong fieldAddress = GetPointerAddress() + (ulong)tuple.Item2;

            return Create(fieldType, fieldAddress, fieldName, GenerateNewName(".{0}", fieldName));
        }

        /// <summary>
        /// Gets the class field as T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public T GetClassField<T>(string fieldName)
        {
            Variable field = GetClassField(fieldName);

            if (field == null)
            {
                return default(T);
            }

            return field.CastAs<T>();
        }

        /// <summary>
        /// Gets the field value.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <returns>Field variable if the specified field exists.</returns>
        public Variable GetField(string name)
        {
            return GetUserTypeCastedFieldByName(name);
        }

        /// <summary>
        /// Gets the original field value (not user type casted field).
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <returns>Field variable if the specified field exists.</returns>
        private Variable GetOriginalField(string name)
        {
            CodeType fieldType = codeType.GetFieldType(name);
            ulong fieldAddress = GetPointerAddress() + (ulong)codeType.GetFieldOffset(name);

            return CreateNoCast(fieldType, fieldAddress, name, GenerateNewName(".{0}", name));
        }

        /// <summary>
        /// Gets the user type casted field.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <returns>Field variable casted to user type if the specified field exists.</returns>
        private Variable GetUserTypeCastedFieldByName(string name)
        {
            return codeType.Module.Process.CastVariableToUserType(GetOriginalField(name));
        }

        /// <summary>
        /// Casts the variable to user type.
        /// </summary>
        /// <param name="originalVariable">The original variable.</param>
        internal static Variable CastVariableToUserType(Variable originalVariable)
        {
            if (Context.UserTypeMetadata == null || Context.UserTypeMetadata.Length == 0)
            {
                return originalVariable;
            }

            // Get user type descriptions to be used by this process
            var userTypes = originalVariable.codeType.Module.Process.UserTypes;

            // Look at the type and see if it should be converted to user type
            var typesBasedOnModule = userTypes.Where(t => t.Module == originalVariable.codeType.Module);
            var typesBasedOnName = typesBasedOnModule.Where(t => t.UserType.TypeId == (originalVariable.codeType.IsPointer ? originalVariable.codeType.ElementType.TypeId : originalVariable.codeType.TypeId));

            var types = typesBasedOnName.ToArray();

            if (types.Length > 1)
            {
                throw new Exception(string.Format("Multiple types ({0}) are targeting same type {1}", string.Join(", ", types.Select(t => t.Type.FullName)), originalVariable.codeType.Name));
            }

            if (types.Length == 0)
            {
                return originalVariable;
            }

            // Check if it is null
            if (originalVariable.GetPointerAddress() == 0)
            {
                return null;
            }

            // Create new instance of user defined type
            var activator = GlobalCache.UserTypeDelegates[types[0].Type].SymbolicConstructor;

            return (Variable)activator(originalVariable);
        }

        /// <summary>
        /// Casts the variable collection to user type.
        /// </summary>
        /// <param name="originalCollection">The original variable collection.</param>
        internal static VariableCollection CastVariableCollectionToUserType(VariableCollection originalCollection)
        {
            Variable[] variables = new Variable[originalCollection.Count];
            Dictionary<string, Variable> variablesByName = new Dictionary<string, Variable>();

            for (int i = 0; i < variables.Length; i++)
            {
                variables[i] = originalCollection[i].codeType.Module.Process.CastVariableToUserType(originalCollection[i]);
                variablesByName.Add(originalCollection[i].name, variables[i]);
            }

            return new VariableCollection(variables, variablesByName);
        }

        /// <summary>
        /// Gets the pointer to this variable.
        /// </summary>
        public Variable GetPointer()
        {
            return CreatePointerNoCast(codeType.PointerToType, GetPointerAddress(), name, path);
        }

        /// <summary>
        /// Gets the length of the array represented with this variable.
        /// </summary>
        /// <exception cref="System.ArgumentException">Variable is not an array, but  + type.Name</exception>
        public int GetArrayLength()
        {
            if (!codeType.IsArray)
            {
                throw new ArgumentException("Variable is not an array, but " + codeType.Name);
            }

            return (int)(codeType.Size / codeType.ElementType.Size);
        }

        /// <summary>
        /// Tries to convert the variable to the specified type.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="result">The result.</param>
        /// <returns><c>true</c> if conversion succeeds, <c>false</c> otherwise</returns>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            // TODO: Implement
            return base.TryConvert(binder, out result);
        }

        /// <summary>
        /// Tries apply binary operation on the variable.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="arg">The argument.</param>
        /// <param name="result">The result.</param>
        /// <returns><c>true</c> if operation succeeds, <c>false</c> otherwise</returns>
        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result)
        {
            // TODO: Implement
            return base.TryBinaryOperation(binder, arg, out result);
        }

        /// <summary>
        /// Tries to apply unary operation on the variable.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="result">The result.</param>
        /// <returns><c>true</c> if operation succeeds, <c>false</c> otherwise</returns>
        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result)
        {
            // TODO: Implement
            return base.TryUnaryOperation(binder, out result);
        }

        /// <summary>
        /// Gets the dynamic member names.
        /// </summary>
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return GetFieldNames();
        }

        /// <summary>
        /// Tries to get the member.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="result">The result.</param>
        /// <returns><c>true</c> if member exists, <c>false</c> otherwise</returns>
        private bool TryGetMember(string name, out object result)
        {
            try
            {
                result = GetUserTypeCastedFieldByName(name);
                return true;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Tries to get the member.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="result">The result.</param>
        /// <returns><c>true</c> if member exists, <c>false</c> otherwise</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryGetMember(binder.Name, out result);
        }

        /// <summary>
        /// Tries to get the element at specified index.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="indexes">The indexes.</param>
        /// <param name="result">The result.</param>
        /// <returns><c>true</c> if index exists, <c>false</c> otherwise</returns>
        /// <exception cref="System.ArgumentException">Multidimensional arrays are not supported</exception>
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes.Length > 1)
            {
                throw new ArgumentException("Multidimensional arrays are not supported");
            }

            try
            {
                int index = Convert.ToInt32(indexes[0]);

                if (codeType.IsPointer || codeType.IsArray)
                {
                    result = GetArrayElement(index);
                    return true;
                }
            }
            catch (Exception)
            {
                // Index is not a number, fall back to getting the member
            }

            return TryGetMember(indexes[0].ToString(), out result);
        }

#region Forbidden setters/deleters
        /// <summary>
        /// Tries to delete the member - it is forbidden.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public override bool TryDeleteMember(DeleteMemberBinder binder)
        {
            throw new UnauthorizedAccessException();
        }

        /// <summary>
        /// Tries to delete the index - it is forbidden.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="indexes">The indexes.</param>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes)
        {
            throw new UnauthorizedAccessException();
        }

        /// <summary>
        /// Tries to set the member - it is forbidden.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            throw new UnauthorizedAccessException();
        }

        /// <summary>
        /// Tries to set the index - it is forbidden.
        /// </summary>
        /// <param name="binder">The binder.</param>
        /// <param name="indexes">The indexes.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.UnauthorizedAccessException"></exception>
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            throw new UnauthorizedAccessException();
        }
#endregion

        /// <summary>
        /// Gets field offset.
        /// </summary>
        public int GetFieldOffset(string fieldName)
        {
            return codeType.GetFieldOffset(fieldName);
        }

        /// <summary>
        /// Finds the fields.
        /// </summary>
        private Variable[] FindFields()
        {
            if (codeType.IsArray)
            {
                return new Variable[0];
            }

            string[] fieldNames = GetFieldNames();
            Variable[] fields = new Variable[fieldNames.Length];

            for (int i = 0; i < fieldNames.Length; i++)
            {
                fields[i] = GetOriginalField(fieldNames[i]);
            }

            return fields;
        }

        /// <summary>
        /// Finds the user type casted fields.
        /// </summary>
        private Variable[] FindUserTypeCastedFields()
        {
            Variable[] originalFields = GetOriginalFields();
            Variable[] fields = new Variable[originalFields.Length];

            for (int i = 0; i < originalFields.Length; i++)
            {
                fields[i] = codeType.Module.Process.CastVariableToUserType(originalFields[i]);
            }

            return fields;
        }

        /// <summary>
        /// Reads the data value of this variable.
        /// </summary>
        private ulong ReadData()
        {
            return Context.SymbolProvider.ReadSimpleData(codeType, Address);
        }

        /// <summary>
        /// Generates the new variable name.
        /// If existing name is computed, it will remain like that. If not, new format will be appended to existing name.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        private string GenerateNewName(string format, params object[] args)
        {
            if (!Context.EnableVariablePathTracking)
            {
                return UntrackedPath;
            }

            if (name == ComputedName)
            {
                return name;
            }

            return name + string.Format(format, args);
        }

#region IConvertible
        public TypeCode GetTypeCode()
        {
            throw new NotImplementedException();
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return (bool)this;
        }

        public char ToChar(IFormatProvider provider)
        {
            return (char)this;
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return (sbyte)this;
        }

        public byte ToByte(IFormatProvider provider)
        {
            return (byte)this;
        }

        public short ToInt16(IFormatProvider provider)
        {
            return (short)this;
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return (ushort)this;
        }

        public int ToInt32(IFormatProvider provider)
        {
            return (int)this;
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return (uint)this;
        }

        public long ToInt64(IFormatProvider provider)
        {
            return (long)this;
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return (ulong)this;
        }

        public float ToSingle(IFormatProvider provider)
        {
            return (float)this;
        }

        public double ToDouble(IFormatProvider provider)
        {
            return (double)this;
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        public string ToString(IFormatProvider provider)
        {
            return ToString();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return CastAs(conversionType);
        }
#endregion
    }
}
