using System;
using System.Collections;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class TernaryOperatorPointless
    {
        public TernaryOperatorPointless(bool b  )
        {
            var x = true; // Noncompliant; is this what was intended?
            var y = 1> 18 ? true : false;
            y = true; //Noncompliant
            new TernaryOperatorPointless(true);//Noncompliant
        }
    }
}
