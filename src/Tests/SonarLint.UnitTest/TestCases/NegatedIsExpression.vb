Module Module1
    Sub Main()
        Dim a = Not "a" Is Nothing ' Noncompliant
        a = Not "a" Is ' Noncompliant
            Nothing 'some comment
        a = "a" IsNot Nothing ' Compliant
    End Sub
End Module