using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tests.Diagnostics
{
    public class TaskConfigureAwait
    {
        public async void Test()
        {
            await Task.Delay(1000); // Noncompliant
//                ^^^^^^^^^^^^^^^^
            await Task.Delay(1000).ConfigureAwait(false); // Compliant
            await Task.Delay(1000).ConfigureAwait(true); // Compliant, we assume that there is a reason to explicitly specify context switching

            var t = Task.Delay(1000);
        }
    }
}
