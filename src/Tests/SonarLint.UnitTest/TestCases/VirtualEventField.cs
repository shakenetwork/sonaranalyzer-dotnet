using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class VirtualEventField
    {
        public virtual event EventHandler OnRefueled; // Noncompliant

        public virtual event EventHandler Foo
        {
            add
            {
                Console.WriteLine("Base Foo.add called");
            }
            remove
            {
                Console.WriteLine("Base Foo.remove called");
            }
        }
    }
}
