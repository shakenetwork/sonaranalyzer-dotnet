using System.Collections.Generic;
using System.IO;

namespace Tests.Diagnostics
{
    public class StreamReadStatement
    {
        public StreamReadStatement(string fileName)
        {
            using (var stream = File.Open(fileName, FileMode.Open))
            {
                var result = new byte[stream.Length];
                stream.Read(result, 0, (int)stream.Length); // Noncompliant
//              ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                var l = stream.Read(result, 0, (int)stream.Length);
                stream.ReadAsync(result, 0, (int)stream.Length); // Noncompliant {{Check the return value of the "ReadAsync" call to see how many bytes were read.}}
                await stream.ReadAsync(result, 0, (int)stream.Length); // Noncompliant
                stream.Write(result, 0, (int)stream.Length);
            }
        }
    }
}
