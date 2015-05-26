using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    public class EmptinessChecking
    {
        private static bool HasContent1(IEnumerable<string> l)
        {
            return l.Count() > 0; // Noncompliant
        }

        private static bool HasContent4(IEnumerable<string> l)
        {
            return 0 < Enumerable.Count(l); // Noncompliant
        }
        private static bool HasContent2(List<string> l)
        {
            return Enumerable.Count(l) >= 1; // Noncompliant
        }
        private static bool HasContent3(List<string> l)
        {
            return l.Any();
        }
        private static bool IsEmpty1(List<string> l)
        {
            return l.Count() == 0; // Noncompliant
        }
        private static bool IsEmpty2(List<string> l)
        {
            return l.Count() <= 0; // Noncompliant
        }
        private static bool IsEmpty3(List<string> l)
        {
            return !l.Any(); 
        }
    }
}
