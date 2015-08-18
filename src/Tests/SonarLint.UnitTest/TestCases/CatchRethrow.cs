using System;

namespace Tests.TestCases
{
    class CatchRethrow
    {
        private void doSomething() { throw new NotSupportedException(); }
        public void Test()
        {
            try
            {
                doSomething();
            }
            catch (Exception exc) //Noncompliant
            {
                throw;
            }

            try
            {
                doSomething();
            }
            catch (ArgumentException) //Noncompliant
            {
                throw;
            }

            try
            {
                doSomething();
            }
            catch (ArgumentException) // Compliant now, but if the following catch is removed it is not.
            // So, this will also be removed by the codefix, as it is doing an iterative run
            {
                throw;
            }
            catch (NotSupportedException) //Noncompliant
            {
                throw;
            }

            try
            {
                doSomething();
            }
            catch (ArgumentException)
            {
                Console.WriteLine("");
                throw;
            }
            catch (Exception)
            {
                Console.WriteLine("");
                throw;
            }

            try
            {
                doSomething();
            }
            catch (NotSupportedException) //Noncompliant
            {
                throw;
            }
            finally
            {

            }
        }
    }
}
