Module Module1
    Sub Main(x As Boolean)
        Dim a = "a" IsNot Nothing ' Noncompliant
        a = "a" IsNot ' Noncompliant
            Nothing 'some comment
        a = "a" IsNot Nothing ' Compliant
        Main("a" IsNot Nothing) ' Noncompliant
    End Sub
End Module