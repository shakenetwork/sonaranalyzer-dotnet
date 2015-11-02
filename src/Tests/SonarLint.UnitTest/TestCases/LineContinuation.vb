Module Module1
    Sub LineContinuation()
        ' Noncompliant@+1
        Console.WriteLine("Hello" _
                          & "world")
    End Sub
End Module