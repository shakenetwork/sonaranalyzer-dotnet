Imports System.Collections.Generic

Namespace Tests.Diagnostics
    Public Class TabCharacter

        Public Sub New()
            Dim tabs = "	" ' Noncompliant
            Dim tabs2 = "		"
            ' some more tabs: "		"
        End Sub
    End Class
End Namespace

