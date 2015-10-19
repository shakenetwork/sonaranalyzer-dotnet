Module Module1
    Sub Main()
        Dim foo = {"a", "b", "c"} ' Noncompliant
        foo = New String() {} ' Compliant
        Dim foo2 = {}
        foo2 = {"a", "b", "c"}
        Dim foo3 = New A() {New B()} ' Compliant
        foo3 = {New B(), New A()} ' Noncompliant
    End Sub

End Module
Class A

End Class
Class B
    Inherits A

End Class