using System;
namespace Tests.Diagnostics
{
    [System.Flags]
    enum FruitType    // Noncompliant
//       ^^^^^^^^^
    {
        Banana,
        Orange = 5,
        Strawberry
    }
    enum FruitType2    // Compliant
    {
        Banana,
        Orange,
        Strawberry
    }

    [Flags]
    enum FruitType3    // Compliant
    {
        Banana=1,
        Orange =4,
        Strawberry =5
    }
}
