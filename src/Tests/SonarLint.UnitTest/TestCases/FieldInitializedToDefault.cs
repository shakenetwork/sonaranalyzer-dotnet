using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    struct Dummy
    { }

    class FieldInitializedToDefault<T>
    {
        public const int myConst = 0; //Compliant
        public double fieldD1 = 0; // Noncompliant
        public double fieldD2 = +0.0; // Noncompliant
        public double fieldD2b = -+-+-0.0; // Noncompliant
        public double fieldD3 = .0; // Noncompliant
        public decimal fieldD4 = .0m; // Noncompliant
        public decimal fieldD5 = .2m;
        public byte b = 0; // Noncompliant
        public char c = 0; // Noncompliant
        public bool bo = false; // Noncompliant
        public sbyte sb = +0; // Noncompliant
        public ushort us = -0; // Noncompliant
        public uint ui = +-+-+0U; // Noncompliant
        public ulong ul = 0UL; // Noncompliant
    }
}
