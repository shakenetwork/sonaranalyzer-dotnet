namespace Tests.Diagnostics
{
    public class C1
    {
        public virtual void MyNotOverridenMethod() { }
    }
    internal class Partial1Part //Noncompliant
    {
        void Method() { }
        public void MyNotOverridenMethod() { } //Noncompliant
        public int Prop { get; set; } //Noncompliant
    }
    struct PartialStruct //Noncompliant
    {
    }
interface PartialInterface //Noncompliant
    {
    }

    internal partial class Partial2Part
    {
    }

    internal partial class Partial2Part
    {
        public virtual void MyOverridenMethod() { }
        public virtual int Prop { get; set; }
    }
    internal class Override : Partial2Part
    {
        public override void MyOverridenMethod() { }
    }
    sealed class SealedClass : Partial2Part
    {
        public void MyNotOverridenMethod() { } //Noncompliant, reported by VS as well, but we need it for the codefix
        public override void MyOverridenMethod() { } //Noncompliant
        public override int Prop { get; set; } //Noncompliant
    }

    internal class BaseClass<T>
    {
        public virtual string Process(string input)
        {
            return input;
        }
    }

    internal class SubClass : BaseClass<string>
    {
        public override string Process(string input)
        {
            return "Test";
        }
    }
}
