using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    struct Dummy
    { }

    class MemberInitializedToDefault<T>
    {
        public const int myConst = 0; //Compliant
        public double fieldD1; // Noncompliant
        public double fieldD2; // Noncompliant
        public double fieldD2b; // Noncompliant
        public double fieldD3; // Noncompliant
        public decimal fieldD4; // Noncompliant
        public decimal fieldD5 = .2m;
        public byte b; // Noncompliant
        public char c; // Noncompliant
        public bool bo; // Noncompliant
        public sbyte sb; // Noncompliant
        public ushort us; // Noncompliant
        public uint ui; // Noncompliant
        public ulong ul; // Noncompliant

        public static object o; // Noncompliant
        public object MyProperty { get; set; } // Noncompliant
        public object MyProperty2 { get { return null; } set { } } = null;

        public event EventHandler MyEvent;  // Noncompliant
        public event EventHandler MyEvent2 = (s, e) => { };
    }
}
