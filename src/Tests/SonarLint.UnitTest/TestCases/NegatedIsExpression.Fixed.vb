Module Module1
    Sub Main()
        Dim a = "a" IsNot Nothing ' Noncompliant
        a = "a" IsNot ' Noncompliant
            Nothing 'some comment
        a = "a" IsNot Nothing ' Compliant
    End Sub
End Module