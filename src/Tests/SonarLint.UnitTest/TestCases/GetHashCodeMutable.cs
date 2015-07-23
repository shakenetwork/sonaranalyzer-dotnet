using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace Tests.Diagnostics
{
    public class AnyOther
    {
        public int Field;
    }

    public class GetHashCodeMutable : AnyOther
    {
        public readonly DateTime birthday;
        public const int Zero = 0;
        public int age;
        public string name;

        public GetHashCodeMutable()
        {
            any = new AnyOther();
        }

        public override int GetHashCode()
        {
            int hash = Zero;
            hash += age.GetHashCode(); // Noncompliant
            hash += this.name.GetHashCode(); // Noncompliant
            hash += this.birthday.GetHashCode();
            hash += Field; // Noncompliant
            return hash;
        }
        public int SomeMethod()
        {
            int hash = Zero;
            hash += this.age.GetHashCode();
            return hash;
        }
    }
}
