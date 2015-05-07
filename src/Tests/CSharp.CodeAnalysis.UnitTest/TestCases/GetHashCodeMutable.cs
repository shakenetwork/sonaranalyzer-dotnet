using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class GetHashCodeMutable
    {
        public readonly DateTime birthday;
        public const int Zero = 0;
        public int age;
        public string name;

        public override int GetHashCode()
        {
            int hash = Zero;
            hash += age.GetHashCode(); // Noncompliant
            hash += this.name.GetHashCode(); // Noncompliant
            hash += this.birthday.GetHashCode();
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
