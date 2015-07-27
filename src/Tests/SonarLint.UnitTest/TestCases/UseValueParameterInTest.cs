using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    class UseValueParameter
    {
        private int count;
        public int Count
        {
            get { return count; }
            set { count = 3; }
        }
        public int Count2
        {
            get { return count; }
            set { count = value; }
        }
        public int Count3
        {
            //get { return count; }
            set
            {
                var value = 5;
                count = value;
            }
        }
        public int Count5
        {
            set
            {
                throw new Exception();
            }
        }

        public int Count4 => count;

        public int this[int i]
        {
            get
            {
                return 0;
            }
            set
            {
                var x = 1;
            }
        }

        event EventHandler PreDrawEvent;

        event EventHandler IDrawingObject.OnDraw
        {
            add
            {
                lock (PreDrawEvent)
                {
                }
            }
            remove
            {
                lock (PreDrawEvent)
                {
                    PreDrawEvent -= value;
                }
            }
        }
    }
}
