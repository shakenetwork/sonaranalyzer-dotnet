using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{

    class SillyBitwiseOperation
    {
        static void Main(string[] args  )
        {
            int result;
            int bitMask = 0x010F;

            result = bitMask; // Noncompliant
            result = bitMask;  // Noncompliant
            result = bitMask;  // Noncompliant
            result = bitMask;  // Noncompliant
            var result2 = result;  // Noncompliant

            result = bitMask & 1; // Compliant
            result = bitMask | 1; // compliant
            result = bitMask ^ 1; // Compliant
            result &= 1; // Compliant
            result |= 1; // compliant
            result ^= 1; // Compliant

            long bitMaskLong = 0x010F;
            long resultLong;
            resultLong = bitMaskLong; // Noncompliant
            resultLong = bitMaskLong & 0L; // Compliant
            resultLong = bitMaskLong; // Noncompliant
            resultLong = bitMaskLong; // Noncompliant
            resultLong = bitMaskLong & returnLong(); // Compliant
            resultLong = bitMaskLong & 0x0F; // Compliant

            var resultULong = 1UL; // Noncompliant
            resultULong = 1UL | 18446744073709551615UL; // Compliant
        }
        private static long returnLong()
        {
            return 1L;
        }
    }
}
