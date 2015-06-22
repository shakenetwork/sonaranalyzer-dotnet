using System;
using System.Collections.Generic;
using System.Linq;

namespace Tests.Diagnostics
{
    public class SimplifyLinq
    {
        public SimplifyLinq(List<object> coll)
        {
            var x = coll.Select(element => element as object).Any(element => element != null);  // Noncompliant use OfType
            x = coll.Select((element) => ((element as object))).Any(element => (element != null) && CheckCondition(element) && true);  // Noncompliant use OfType
            var y = coll.Where(element => element is object).Select(element => element as object); // Noncompliant use OfType
            var y = coll.Where(element => element is object).Select(element => element as object[]);
            y = coll.Where(element => element is object).Select(element => (object)element); // Noncompliant use OfType
            x = coll.Where(element => element == null).Any();  // Noncompliant use Any([expression])
            var z = coll.Where(element => element == null).Count();  // Noncompliant use Count([expression])
            z = Enumerable.Count(coll.Where(element => element == null));  // Noncompliant
            z = Enumerable.Count(Enumerable.Where(coll, element => element == null));  // Noncompliant
            y = coll.Select(element => element as object);
            
            x = Enumerable.Select(coll, element => element as object).Any(element => element != null); //Noncompliant
            x = Enumerable.Any(Enumerable.Select(coll, element => element as object), element => element != null); //Noncompliant
        }

        public bool CheckCondition(object x)
        {
            return true;
        }
    }
}
