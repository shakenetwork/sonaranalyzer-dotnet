namespace Tests.Diagnostics
{
    static class IEnumerableExtensions // Compliant
    {

    }

    class foo // Noncompliant
    {
    }

    partial class Foo
    {
    }

    class I // Noncompliant
    {
    }

    class IFoo // Noncompliant
    {
    }

    class IdentityFoo
    {
    }

    partial class
    Foo
    {
    }

    partial class
    IBar // Noncompliant
    {
    }

    partial class
    AbClass_Bar // Noncompliant
    {
    }
}
