using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class StringConcatenationInLoop
    {
        public StringConcatenationInLoop()
        {
            string s = "";
            int i = 0;
            for (int i = 0; i < 50; i++)
            {
                var sLoop = "";

                s = s + "a";  // Noncompliant
//              ^^^^^^^^^^^
                s += "a";     // Noncompliant
                sLoop += "a"; // Compliant

                i += 5;
            }
            s += "a";

        }
    }
}
