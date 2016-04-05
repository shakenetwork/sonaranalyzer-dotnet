/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace SonarLint.Helpers
{
    public sealed class KnownType
    {
        #region Known types

        public static readonly KnownType System_Nullable_T = new KnownType(SpecialType.System_Nullable_T, "System.Nullable<T>");
        public static readonly KnownType System_Collections_Generic_IEnumerable_T = new KnownType(SpecialType.System_Collections_Generic_IEnumerable_T, "System.Collections.Generic.IEnumerable<T>");
        public static readonly KnownType System_Collections_IEnumerable = new KnownType(SpecialType.System_Collections_IEnumerable, "System.Collections.IEnumerable");
        public static readonly KnownType System_IDisposable = new KnownType(SpecialType.System_IDisposable, "System.IDisposable");
        public static readonly KnownType System_Array = new KnownType(SpecialType.System_Array, "System.Array");
        public static readonly KnownType System_Collections_Generic_IList_T = new KnownType(SpecialType.System_Collections_Generic_IList_T, "System.Collections.Generic.IList<T>");

        public static readonly KnownType System_Object = new KnownType(SpecialType.System_Object, "object");
        public static readonly KnownType System_String = new KnownType(SpecialType.System_String, "string");
        public static readonly KnownType System_Boolean = new KnownType(SpecialType.System_Boolean, "bool");
        public static readonly KnownType System_Int64 = new KnownType(SpecialType.System_Int64, "long");
        public static readonly KnownType System_Int32 = new KnownType(SpecialType.System_Int32, "int");
        public static readonly KnownType System_Int16 = new KnownType(SpecialType.System_Int16, "short");
        public static readonly KnownType System_UInt64 = new KnownType(SpecialType.System_UInt64, "ulong");
        public static readonly KnownType System_UInt32 = new KnownType(SpecialType.System_UInt32, "uint");
        public static readonly KnownType System_UInt16 = new KnownType(SpecialType.System_UInt16, "ushort");
        public static readonly KnownType System_Decimal = new KnownType(SpecialType.System_Decimal, "decimal");
        public static readonly KnownType System_Single = new KnownType(SpecialType.System_Single, "float");
        public static readonly KnownType System_Double = new KnownType(SpecialType.System_Double, "double");
        public static readonly KnownType System_Char = new KnownType(SpecialType.System_Char, "char");
        public static readonly KnownType System_Byte = new KnownType(SpecialType.System_Byte, "byte");
        public static readonly KnownType System_SByte = new KnownType(SpecialType.System_SByte, "sbyte");
        public static readonly KnownType System_DateTime = new KnownType(SpecialType.System_DateTime, "DateTime");

        public static readonly ISet<KnownType> FloatingPointNumbers = new HashSet<KnownType>(new[]
        {
            System_Single,
            System_Double
        });
        public static readonly ISet<KnownType> NonIntegralNumbers = new HashSet<KnownType>(new[]
        {
            System_Single,
            System_Double,
            System_Decimal
        });
        public static readonly ISet<KnownType> UnsignedIntegers = new HashSet<KnownType>(new[]
        {
            System_UInt64,
            System_UInt32,
            System_UInt16
        });
        public static readonly ISet<KnownType> IntegralNumbers = new HashSet<KnownType>(new[]
        {
            System_Int16,
            System_Int32,
            System_Int64,
            System_UInt16,
            System_UInt32,
            System_UInt64,
            System_Char,
            System_Byte,
            System_SByte
        });

        public static readonly KnownType System_Exception = new KnownType("System.Exception");
        public static readonly KnownType System_Type = new KnownType("System.Type");
        public static readonly KnownType System_GC = new KnownType("System.GC");
        public static readonly KnownType System_IFormatProvider = new KnownType("System.IFormatProvider");
        public static readonly KnownType System_FlagsAttribute = new KnownType("System.FlagsAttribute");
        public static readonly KnownType System_ThreadStaticAttribute = new KnownType("System.ThreadStaticAttribute");
        public static readonly KnownType System_Console = new KnownType("System.Console");
        public static readonly KnownType System_Collections_IList = new KnownType("System.Collections.IList");
        public static readonly KnownType System_Collections_Generic_List_T = new KnownType("System.Collections.Generic.List<T>");
        public static readonly KnownType System_EventArgs = new KnownType("System.EventArgs");

        public static readonly KnownType System_IO_FileStream = new KnownType("System.IO.FileStream");
        public static readonly KnownType System_IO_StreamReader = new KnownType("System.IO.StreamReader");
        public static readonly KnownType System_IO_StreamWriter = new KnownType("System.IO.StreamWriter");
        public static readonly KnownType System_IO_Stream = new KnownType("System.IO.Stream");

        public static readonly KnownType System_Net_WebClient = new KnownType("System.Net.WebClient");

        public static readonly KnownType System_Net_Sockets_TcpClient = new KnownType("System.Net.Sockets.TcpClient");
        public static readonly KnownType System_Net_Sockets_TcpListener = new KnownType("System.Net.Sockets.TcpListener");
        public static readonly KnownType System_Net_Sockets_UdpClient = new KnownType("System.Net.Sockets.UdpClient");

        public static readonly KnownType System_Drawing_Image = new KnownType("System.Drawing.Image");
        public static readonly KnownType System_Drawing_Bitmap = new KnownType("System.Drawing.Bitmap");

        public static readonly KnownType System_Runtime_InteropServices_DefaultParameterValueAttribute = new KnownType("System.Runtime.InteropServices.DefaultParameterValueAttribute");
        public static readonly KnownType System_Runtime_InteropServices_OptionalAttribute = new KnownType("System.Runtime.InteropServices.OptionalAttribute");
        public static readonly KnownType System_ComponentModel_DefaultValueAttribute = new KnownType("System.ComponentModel.DefaultValueAttribute");

        public static readonly KnownType System_Globalization_CultureInfo = new KnownType("System.Globalization.CultureInfo");
        public static readonly KnownType System_Globalization_CompareOptions = new KnownType("System.Globalization.CompareOptions");
        public static readonly KnownType System_StringComparison = new KnownType("System.StringComparison");

        public static readonly KnownType System_Security_Cryptography_DES = new KnownType("System.Security.Cryptography.DES");
        public static readonly KnownType System_Security_Cryptography_TripleDES = new KnownType("System.Security.Cryptography.TripleDES");
        public static readonly KnownType System_Security_Cryptography_HashAlgorithm = new KnownType("System.Security.Cryptography.HashAlgorithm");
        public static readonly KnownType System_Security_Cryptography_SHA1 = new KnownType("System.Security.Cryptography.SHA1");
        public static readonly KnownType System_Security_Cryptography_MD5 = new KnownType("System.Security.Cryptography.MD5");

        public static readonly KnownType System_Reflection_Assembly = new KnownType("System.Reflection.Assembly");
        public static readonly KnownType System_Reflection_MemberInfo = new KnownType("System.Reflection.MemberInfo");
        public static readonly KnownType System_Reflection_Module = new KnownType("System.Reflection.Module");
        public static readonly KnownType System_Data_Common_CommandTrees_DbExpression = new KnownType("System.Data.Common.CommandTrees.DbExpression");
        public static readonly KnownType System_Windows_DependencyObject = new KnownType("System.Windows.DependencyObject");

        public static readonly KnownType System_Collections_Immutable_ImmutableArray = new KnownType("System.Collections.Immutable.ImmutableArray");
        public static readonly KnownType System_Collections_Immutable_ImmutableArray_T = new KnownType("System.Collections.Immutable.ImmutableArray<T>");
        public static readonly KnownType System_Collections_Immutable_ImmutableDictionary = new KnownType("System.Collections.Immutable.ImmutableDictionary");
        public static readonly KnownType System_Collections_Immutable_ImmutableDictionary_TKey_TValue = new KnownType("System.Collections.Immutable.ImmutableDictionary<TKey, TValue>");
        public static readonly KnownType System_Collections_Immutable_ImmutableHashSet = new KnownType("System.Collections.Immutable.ImmutableHashSet");
        public static readonly KnownType System_Collections_Immutable_ImmutableHashSet_T = new KnownType("System.Collections.Immutable.ImmutableHashSet<T>");
        public static readonly KnownType System_Collections_Immutable_ImmutableList = new KnownType("System.Collections.Immutable.ImmutableList");
        public static readonly KnownType System_Collections_Immutable_ImmutableList_T = new KnownType("System.Collections.Immutable.ImmutableList<T>");
        public static readonly KnownType System_Collections_Immutable_ImmutableQueue = new KnownType("System.Collections.Immutable.ImmutableQueue");
        public static readonly KnownType System_Collections_Immutable_ImmutableQueue_T = new KnownType("System.Collections.Immutable.ImmutableQueue<T>");
        public static readonly KnownType System_Collections_Immutable_ImmutableSortedDictionary = new KnownType("System.Collections.Immutable.ImmutableSortedDictionary");
        public static readonly KnownType System_Collections_Immutable_ImmutableSortedDictionary_TKey_TValue = new KnownType("System.Collections.Immutable.ImmutableSortedDictionary<TKey, TValue>");
        public static readonly KnownType System_Collections_Immutable_ImmutableSortedSet = new KnownType("System.Collections.Immutable.ImmutableSortedSet");
        public static readonly KnownType System_Collections_Immutable_ImmutableSortedSet_T = new KnownType("System.Collections.Immutable.ImmutableSortedSet<T>");
        public static readonly KnownType System_Collections_Immutable_ImmutableStack = new KnownType("System.Collections.Immutable.ImmutableStack");
        public static readonly KnownType System_Collections_Immutable_ImmutableStack_T = new KnownType("System.Collections.Immutable.ImmutableStack<T>");

        public static readonly KnownType System_Diagnostics_Contracts_PureAttribute = new KnownType("System.Diagnostics.Contracts.PureAttribute");

        public static readonly KnownType System_Runtime_InteropServices_ComImportAttribute = new KnownType("System.Runtime.InteropServices.ComImportAttribute");
        public static readonly KnownType System_Runtime_InteropServices_InterfaceTypeAttribute = new KnownType("System.Runtime.InteropServices.InterfaceTypeAttribute");

        public static readonly KnownType System_Threading_Tasks_Task = new KnownType("System.Threading.Tasks.Task");

        #endregion

        public string TypeName { get; }
        private SpecialType SpecialType { get; }
        private bool IsSpecialType { get; }

        private KnownType(string typeName)
        {
            TypeName = typeName;
            SpecialType = SpecialType.None;
            IsSpecialType = false;
        }

        private KnownType(SpecialType specialType, string typeName)
        {
            TypeName = typeName;
            SpecialType = specialType;
            IsSpecialType = true;
        }

        internal bool Matches(string type)
        {
            return !IsSpecialType && TypeName == type;
        }

        internal bool Matches(SpecialType type)
        {
            return IsSpecialType && SpecialType == type;
        }
    }
}
