using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class TernaryOperatorPointless
    {
        public TernaryOperatorPointless()
        {
            var x = 1> 18 ? true : true; // Noncompliant; is this what was intended?
            var y = 1> 18 ? true : false;
            y = 1 > 18 ? (true) : true; //Noncompliant
        }
    }
}
