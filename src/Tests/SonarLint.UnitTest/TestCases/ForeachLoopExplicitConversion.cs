using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    interface I { }
    class A : I { }
    class B : A { }
    class ForeachLoopExplicitConversion
    {
        public void S(string s)
        {
            foreach (var item in s)
            { }
            foreach (int item in s)
            { }
            foreach (A item in s) //Noncompliant
            { }
        }
        public void M1(IEnumerable<int> enumerable)
        {
            foreach (int i in enumerable)
            { }
            foreach (var i in enumerable)
            { }
            foreach (object i in enumerable)
            { }
        }
        public void M2(IEnumerable<A> enumerable)
        {
            foreach (A i in enumerable)
            { }
            foreach (var i in enumerable)
            { }
            foreach (B i in enumerable) // Noncompliant
            { }
        }
        public void M3(A[] array)
        {
            foreach (A i in array)
            { }
            foreach (I i in array)
            { }
            foreach (B i in array) // Noncompliant
            { }
        }
        public void M4(A[][] array)
        {
            foreach (A[] i in array)
            { }
            foreach (object[] i in array)
            { }
            foreach (var i in array)
            { }
            foreach (B[] i in array) // Noncompliant
            { }
        }
        public void M5(ArrayList list)
        {
            foreach (A i in list) // Noncompliant
            { }
            foreach (var i in list)
            { }
            foreach (object i in list)
            { }
            foreach (B i in list) // Noncompliant
            { }
        }
    }
}
