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
            var resource = new Resource(); // Noncompliant; retrieved value not used
            using (resource = new Resource())
            {
                resource.DoSomething();
            }
        }
        
    }
}
