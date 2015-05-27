using System;
using System.Collections.Generic;
using System.IO;

namespace Tests.Diagnostics
{
    public class NotDisposedIDisposableField
    {
        public Stream field1 = new MemoryStream(), field5; //Compliant
        private static Stream field2; //Compliant

        private Stream field3; //Noncompliant
        private Stream field6 = new MemoryStream(); //Noncompliant
        private Stream field4; //Compliant, disposed

        public void Init()
        {
            field3 = new MemoryStream();
            field4 = new MemoryStream();
        }

        public void DoSomething()
        {
            field4.Dispose();
        }
    }
}
