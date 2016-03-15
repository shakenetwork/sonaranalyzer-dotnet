namespace Tests.Diagnostics
{
    public class TooManyLabelsInSwitch
    {
        public enum MyEnum
        {
            A,
            B,
            C,
            D
        }

        public TooManyLabelsInSwitch(int n, MyEnum en)
        {
            switch (n)
            {
                case 0:
                    break;
                default:
                    break;
            }

            switch (n) // Noncompliant
            {
                case 0:
                case 1:
                    break;
                default:
                    break;
            }

            switch (en)
            {
                case MyEnum.A:
                    break;
                case MyEnum.B:
                    break;
                case MyEnum.C:
                    break;
                case MyEnum.D:
                    break;
                default:
                    break;
            }
        }
    }
}
