using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class ReferenceEqualsOnValueType
    {
        public ReferenceEqualsOnValueType()
        {
            var b = object.ReferenceEquals(1, 2); //Noncompliant
            b = ReferenceEquals(1, 2); //Noncompliant
            ReferenceEqualsOnValueType.ReferenceEquals(1, new object()); //Noncompliant
            ReferenceEquals(new object(), new object());
        }
    }
}
