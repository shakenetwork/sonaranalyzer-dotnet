using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class AnonymousDelegateEventUnsubscribe
    {
        public delegate void ChangedEventHandler(object sender, EventArgs e);
        public delegate void ChangedEventHandler2(object sender);
        public event ChangedEventHandler Changed;
        public event ChangedEventHandler2 Changed2;

        void Test()
        {
            Changed += (obj, args) => { };
            Changed -= (obj, args) => { }; //Noncompliant

            Changed -= (obj, args) => Console.WriteLine(); // Noncompliant - single statement
            Changed -= (obj, args) => delegate () { }; // Noncompliant
            Changed2 -= obj => delegate () { }; // Noncompliant

            ChangedEventHandler x = (obj, args) => { };
            Changed -= x;
        }
    }
}
