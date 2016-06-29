using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    public abstract partial class PartialMethodNoImplementation
    {
        partial void Method(); //Noncompliant
//      ^^^^^^^

        void OtherM()
        {
            Method(); //Noncompliant. Will be removed.
            OkMethod();
            OkMethod2();
            M();
        }

        partial void OkMethod();
        partial void OkMethod()
        {
            throw new NotImplementedException();
        }

        partial void OkMethod2()
        {
            throw new NotImplementedException();
        }
        partial void OkMethod2();

        public abstract void M();
    }
}
