using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class HardcodedIpAddress
    {
        [SomeAttribute("127.0.0.1")] // this is mainly for assembly versions
        public HardcodedIpAddress()
        {
            string ip = "127.0.0.1"; // Noncompliant

            ip = "300.0.0.0"; // Compliant, not a valid IP
            ip = "    127.0.0.0    "; // Compliant
            ip = @"    ""127.0.0.0""    "; // Compliant

            ip = "2001:db8:1234:ffff:ffff:ffff:ffff:ffff"; // Noncompliant
            ip = "::/0"; // Compliant, not recognized as IPv6 address
        }
    }
}
