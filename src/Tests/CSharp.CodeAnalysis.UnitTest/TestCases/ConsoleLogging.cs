using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class ConsoleLogging
    {
        private static byte[] GenerateKey(byte[] key)
        {
            GenerateKey2(key);
            Console.WriteLine("debug key = {0}", BitConverter.ToString(key)); //Noncompliant
            System.Diagnostics.Debug.WriteLine("debug key = {0}", BitConverter.ToString(key));
            return key;
        }
    }
}
