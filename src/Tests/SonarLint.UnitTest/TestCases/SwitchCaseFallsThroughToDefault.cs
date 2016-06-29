using System;
using System.Collections.Generic;
using System.IO;

namespace Tests.Diagnostics
{
    public class SwitchCaseFallsThroughToDefault
    {
        public SwitchCaseFallsThroughToDefault(char ch)
        {
            switch (ch)
            {
                case 'b':
                    handleB();
                    break;
                case 'c':  // Noncompliant
//              ^^^^^^^^^
                default:
                    handleTheRest();
                    break;
                case 'a':
                    handleA();
                    break;
            }

            switch (ch)
            {
                case 'b':
                    handleB();
                    break;
                case 'c':  // Noncompliant
                case 'e':  // Noncompliant
                default:
                    handleTheRest();
                    break;
                case 'a':
                    handleA();
                    break;
            }

            switch (ch)
            {
                case 'b':
                    handleB();
                    break;
                case 'c':  // Noncompliant
                default:
                case 'e':  // Noncompliant
                    handleTheRest();
                    break;
                case 'a':
                    handleA();
                    break;
            }
        }
    }
}
