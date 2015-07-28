using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{

    class GetHashCodeEqualsOverride
    {
        private readonly int x;
        public GetHashCodeEqualsOverride(int x)
        {
            this.x = x;
        }
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ base.GetHashCode(); //Noncompliant
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj); //Noncompliant
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    class GetHashCodeEqualsOverride2
    {
        private readonly int x;
        public GetHashCodeEqualsOverride2(int x)
        {
            this.x = x;
        }
        public override int GetHashCode()
        {
            return x.GetHashCode();
        }
    }

    class GetHashCodeEqualsOverride3 : GetHashCodeEqualsOverride2
    {
        private readonly int x;
        public GetHashCodeEqualsOverride3(int x) : base(x)
        {
            this.x = x;
        }
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ base.GetHashCode();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    class Base
    { }

    class Derived : Base
    {
        public override int GetHashCode()
        {
            return base.GetHashCode(); //Noncompliant, calls object.GetHashCode()
        }
    }
}
