using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Tests.Diagnostics
{
    public class Resource : IDisposable
    {
        public void Dispose()
        {
        }
        public int DoSomething()
        {
            return 1;
        }
        public int DoSomethingElse()
        {
            return 5;
        }
    }

    public class DeadStores
    {
        void calculateRate(int a, int b)
        {
            int ll = doSomething(); // Noncompliant


            int i, j;

            i = a + b;
            i += i + 2; // Noncompliant
            i = 5;
            var j = i;  // Noncompliant
            i = doSomething();  // Noncompliant; retrieved value not used
            for (i = 0; i < j + 10; i++)
            {
                //  ...
            }
            i = doSomething();  // Noncompliant; retrieved value not used
            i = doSomething();  // Noncompliant; retrieved value not used
            // ...

            if ((i = doSomething()) == 5 ||
                (i = doSomethingElse()) == 5)
            {
                i += 5; // Noncompliant;
            }

            var resource = new Resource(); // Noncompliant; retrieved value not used
            using (resource = new Resource())
            {
                resource.DoSomething();
            }
        }

        void calculateRate2(int a, int b)
        {
            int i;

            i = doSomething();
            i += a + b;
            storeI(i);

            for (i = 0; i < 10; i++)
            {
                //  ...
            }
        }

        int pow(int a, int b)
        {
            if (b == 0)
            {
                return 0;
            }
            int x = a;
            for (int i = 1; i < b; i++)
            {
                x = x * a;  //Not detected yet, we are in a loop, Dead store because the last return statement should return x instead of returning a
            }
            return a;
        }
        public void Switch()
        {
            var b = 5;
            switch (b)
            {
                case 6:
                    b = 5;
                case 7:
                    b = 56;
            }

            b = 7;
            b += 7; //Noncompliant
        }
    }
}
