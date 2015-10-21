Namespace Tests.Diagnostics

    <System.Flags()>
    Enum FruitType    ' Noncompliant
        Banana
        Orange = 5
        Strawberry
    End Enum

    Enum FruitType2    ' Compliant
        Banana
        Orange
        Strawberry
    End Enum

    <System.Flags()>
    Enum FruitType3
        Banana = 1
        Orange = 3
        Strawberry = 4
    End Enum
End Namespace