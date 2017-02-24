using System.Threading;

namespace Tests.Diagnostics
{
    class Program
    {
        void Foo()
        {
            Thread.CurrentThread.Suspend(); // Noncompliant
            Thread.CurrentThread.Resume(); // Noncompliant

            var thread = Thread.CurrentThread;
            thread.Suspend(); // Noncompliant

            ((((((Thread))).CurrentThread))).Suspend(); // Noncompliant
        }
    }
}