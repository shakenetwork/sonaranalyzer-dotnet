using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.TestCases
{
    internal class C
    {
        private static Dictionary<string, List<int>> Dict;
    }

    class StaticFieldInGenericClass<T/*comment*/, /*comment*/U>
    {
        private static readonly ConditionalWeakTable<T, Task<T>>.CreateValueCallback s_taskCreationCallback = Task.FromResult<T>;

    }
}
