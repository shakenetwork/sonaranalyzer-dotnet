namespace Tests.Diagnostics
{
    public class ExceptionRethrow
    {
        public void Test()
        {
            try
            { }
            catch (ExceptionType1 exc)
            {
                Console.WriteLine(exc);
                throw; // Noncompliant; stacktrace is reset
                throw;
            }
            catch (ExceptionType2 exc)
            {
                throw new Exception("My custom message", exc);  // Compliant; stacktrace preserved
            }

            try
            { }
            catch (Exception)
            {
                throw;
            }

            try
            {
            }
            catch (Exception exception)
            {
                try
                {
                    throw; // Noncompliant
                }
                catch (Exception exc)
                {
                    throw; // Noncompliant
                    throw exception;
                }
            }
        }
    }
}
