using System;

namespace Tests.TestCases
{
    class CatchRethrow
    {
        private void doSomething(  ) { throw new NotSupportedException()  ; }
        public void Test()
        {
            var someWronglyFormatted =      45     ;
            doSomething();
            doSomething();

            try
            {
                doSomething();
            }
            catch (ArgumentException) // Compliant now, but if the following catch is removed it is not.
            // So, this will also be removed by the codefix, as it is doing an iterative run
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
            finally
            {

            }
        }
    }
}
