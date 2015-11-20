using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Tests.Diagnostics
{
    public class AsyncVoidMethod
    {
        async void MyMethod() { } //Noncompliant
        async void MyMethod(object o, EventArgs args) { } //Compliant
    }
}
