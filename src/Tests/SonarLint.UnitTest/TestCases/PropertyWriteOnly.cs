namespace Tests.Diagnostics
{
    public class PropertyWriteOnly
    {
        public int Foo  //Noncompliant
//                 ^^^
        {
            set
            {
                // ... some code ...
            }
        }
        public int Foo2
        {
            get
            {
                return 1;
            }
            set
            {
                // ... some code ...
            }
        }
    }
}
