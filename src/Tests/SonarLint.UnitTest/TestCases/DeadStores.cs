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
            int ll = doSomething(); // Noncompliant; variable not used later

            int i, j;
            i = a + b;
            i += i + 2; // Noncompliant; variable is overwritten in the following statement
            i = 5;
            j = i;
            i = doSomething();  // Noncompliant; retrieved value overwritten in for loop
            for (i = 0; i < j + 10; i++)
            {
                //  ...
            }

            if ((i = doSomething()) == 5 ||
                (i = doSomethingElse()) == 5)   //special case, where i is overwritten in the same statement (if) many times. All of them is ignored
            {
                i += 5; // Noncompliant, last use of i, and we are not in a loop
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
                    b = 5; //compliant, we ignore consecutive statements that are not in the same context (have different parents)
                case 7:
                    b = 56;
            }

            b = 7;
            b += 7; //Noncompliant
        }
        public List<int> Method(int i)
        {
            var l = new List<int>();

            Func<List<int>> func = () =>
            {
                return (l = new List<int>(new [] {i}));
            };

            var x = l; //Noncompliant

            return func();
        }

        public List<int> Method2(int i)
        {
            var l = new List<int>();

            return (() =>
            {
                return (l = new List<int>(new[] { i }));
            })();
        }

        public List<int> Method3(int i)
        {
            bool f = false;
            if (true || (f = false))
            {
                if (f)
                {

                }
            }
        }

        public List<int> Method4(int i)
        {
            bool f;
            f = true;
            if (true || (f = false))
            {
                if (f)
                {

                }
            }
        }
    }
}
