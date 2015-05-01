using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class HardcodedIpAddress
    {
        public HardcodedIpAddress()
        {
            string ip = "127.0.0.1"; // Noncompliant

            ip = "300.0.0.0"; // Compliant, not a valid IP
        }
    }
}
