using System;
using System.Diagnostics;

namespace Tests.Diagnostics
{
    public class ConditionEvaluatesToConstant
    {
        public void Method1()
        {
            var b = true;
            if (b) // Noncompliant
            {
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public void Method2()
        {
            var b = true;
            if (b) // Noncompliant
            {
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public void Method3()
        {
            bool b;
            TryGet(out b);
            if (b) { }
        }
        private void TryGet(out bool b) { b = false; }

        public void Method4()
        {
            var b = true;
            while (b) // Noncompliant
            {
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public void Method5(bool cond)
        {
            while (cond)
            {
                Console.WriteLine();
            }

            var b = true;
            while (b) // Noncompliant
            {
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public void Method6(bool cond)
        {
            var i = 10;
            while (i < 20)
            {
                i = i + 1;
            }

            var b = true;
            while (b) // Non-compliant, the above while changes ProgramState every time we are inside the body, so we exceed the max visited nodes
            {
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public void Method7(bool cond)
        {
            while (true) // Not reporting on this
            {
                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public void Method8(bool cond)
        {
            foreach (var item in new int[][] { { 1,2,3 } })
            {
                foreach (var i in item)
                {
                    Console.WriteLine();
                }
            }
        }

        public void Method9_For(bool cond)
        {
            for (;;) // Not reporting on this
            {

            }
        }

        public void Method_Switch()
        {
            int i = 10;
            bool b = true;
            switch (i)
            {
                case 1:
                default:
                case 2:
                    b = false;
                    break;
                case 3:
                    b = false;
                    break;
            }

            if (b) // Noncompliant b is always false
            {

            }
            else
            { }
        }

        public void Method_Switch_NoDefault()
        {
            int i = 10;
            bool b = true;
            switch (i)
            {
                case 1:
                case 2:
                    b = false;
                    break;
            }

            if (b)
            {

            }
            else
            {

            }
        }

        public void Method_Switch_Learn(bool cond)
        {
            switch (cond)
            {
                case true:
                    if (cond) // Non-compliant, we don't care it's very rare
                    {
                        Console.WriteLine();
                    }
                    break;
            }
        }

        public bool Property1
        {
            get
            {
                var a = new Action(() =>
                {
                    var b = true;
                    if (b) // Noncompliant
                    {
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                });
                return true;
            }
            set
            {
                value = true;
                if (value) // Noncompliant
                {
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }
            }
        }
    }
}
