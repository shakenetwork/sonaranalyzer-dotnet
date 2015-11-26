using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace Tests.Diagnostics
{
    public class StringOperationWithoutCulture
    {
        void Test()
        {
            Test();
            var s = "";
            s = s.ToLower(); // Noncompliant
            s = s.ToUpper(); // Noncompliant
            s = s.ToUpperInvariant();
            s = s.ToUpper(CultureInfo.InvariantCulture);
            s = s.StartsWith("", CultureInfo.InvariantCulture);
            s = s.StartsWith(""); // Compliant, although culture specific
            s = s.EndsWith(""); // Compliant, although culture specific
            s = s.StartsWith("", true, CultureInfo.InvariantCulture);
            var b = s.Equals(""); // Noncompliant
            b = s.Equals(new object());
            b = s.Equals("", StringComparison.CurrentCulture);
            var i = string.Compare("", "", true); // Noncompliant
            i = string.Compare("", 1, "",2,3, true); // Noncompliant
            i = string.Compare("", 1, "",2,3, true, CultureInfo.InstalledUICulture);

            s = 1.8.ToString(); //Noncompliant
            s = 1.8m.ToString("d"); //Noncompliant
            s = 1.8f.ToString("d"); //Noncompliant
            s = 1.8.ToString("d", CultureInfo.InstalledUICulture);

        }
    }
}
