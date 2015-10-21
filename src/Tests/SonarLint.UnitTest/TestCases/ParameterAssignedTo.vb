Imports System

Namespace Tests.Diagnostics

    Module ParameterAssignedToStatic

        <Extension()>
        Static Sub MySub(ByVal a As Integer)
            a = 42 ' Noncompliant
            Try

            Catch exc As Exception
                exc = New Exception() ' Noncompliant
                Dim v As Integer = 5
                v = 6
                Throw exc
            End Try
        End Sub
    End Module

    Public Class ParameterAssignedTo
        Sub f1(a As Integer)
            a = 42 ' Noncompliant
        End Sub
        Sub f2(a As Integer)
            Dim tmp As Integer = a
            tmp = 42
        End Sub
        Sub f3(ByRef a As Integer)
            a = 42
        End Sub
        Sub f4(ByVal a As Integer)
            a = 42 ' Noncompliant
        End Sub
    End Class

End Namespace