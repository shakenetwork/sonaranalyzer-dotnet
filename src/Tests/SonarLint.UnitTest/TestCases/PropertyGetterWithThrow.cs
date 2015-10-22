namespace Tests.Diagnostics
{
    public class PropertyGetterWithThrow
    {
        public int MyProperty
        {
            get
            {
                var x = 5;
                throw new System.NotSupportedException(); //Noncompliant
            }
            set { }
        }
        public int this[int i]
        {
            get
            {
                throw new System.Exception(); // okay
            }
            set
            {
            }
        }
    }
}
