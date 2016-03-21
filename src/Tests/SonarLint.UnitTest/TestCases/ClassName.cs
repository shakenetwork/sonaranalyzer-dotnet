namespace Tests.Diagnostics
{
    class FSM // Noncompliant
    {
    }
    static class IEnumerableExtensions // Compliant
    {

    }

    class foo // Noncompliant
    {
    }

    interface foo // Noncompliant
    {
    }

    interface Foo // Noncompliant
    {
    }

    interface IFoo
    {
    }

    interface IIFoo
    {
    }

    interface I
    {
    }

    interface II
    {
    }

    interface IIIFoo // Noncompliant
    {
    }

    partial class Foo
    {
    }

    class MyClass
    {
        class I
        {
        }
    }

    class IFoo2 // Noncompliant
    {
    }

    class Iden42TityFoo
    {
    }

    partial class
    Foo
    {
    }

    partial class
    AbClass_Bar // Noncompliant
    {
    }

    struct ILMarker // Noncompliant, should be IlMarker
    {

    }

    [System.Runtime.InteropServices.ComImport()]
    internal interface SVsLog  // Compliant
    {
    }

    class A4 { }
    class AA4 { }

    class AbcDEFgh { } // Noncompliant
    class Ab4DEFgh { } // Noncompliant

    class TTTestClassTTT { }// Noncompliant
    class TTT44 { }// Noncompliant
    class ABCDEFGHIJK { }// Noncompliant
    class Abcd4a { }// Noncompliant

    class A_B_C { } // Noncompliant;

    class AB { } // Noncompliant, special case
    class AbABaa { }
    class _AbABaa { } // Noncompliant
}
