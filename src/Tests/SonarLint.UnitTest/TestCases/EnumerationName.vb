Namespace Tests.Diagnostics
    Public Enum MyEnum
        Value
    End Enum
    Public Enum myEnum ' Noncompliant
'               ^^^^^^
        Value
    End Enum
    Public Enum MyEnumTTTT ' Noncompliant
        Value
    End Enum
End Namespace