using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class OptionalParameter
    {
        public OptionalParameter(int i = 0, // Noncompliant
            int j = 1) // Noncompliant
        {
        }
        public OptionalParameter()
        {
        }
        private OptionalParameter(int i = 0) // Compliant, private
        {
        }
    }
}
