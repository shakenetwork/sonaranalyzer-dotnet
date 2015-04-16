using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.TestCases
{
    class SequentialSameContition
    {
        public void doTheThing(object o)
        {
        }

        public int a { get; set; }
        public int b { get; set; }
        public int c { get; set; }
        public void Test()
        {
            if (a == b)
            {
                doTheThing(b);
            }
            if (a == b) // Noncompliant
            {  
                doTheThing(b);
            }
            if (a == b) // Noncompliant
            {
                doTheThing(b);
            }
            if (a == c) 
            {
                doTheThing(c);
            }
        }
        public void TestSw()
        {
            switch (a)
            {
                case 1:
                    break;
            }
            switch (a) // Noncompliant
            {
                case 2:
                    break;
            }
        }
    }
}
