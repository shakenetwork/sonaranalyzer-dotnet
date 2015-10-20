Namespace Tests.Diagnostics
    <System.Flags>
    Enum X
        Zero = 0 'Noncompliant
        One = 1
    End Enum
    <System.Flags>
    Enum Y
        None = 0
    End Enum
End Namespace