Class Foo
    Event BeforeClose() ' Noncompliant
    Event afterClose()  ' Noncompliant
    Event Closing()
    Public Custom Event BeforeClose2 As EventHandler ' Noncompliant
        AddHandler(ByVal value As EventHandler)
            Me.Events.AddHandler(Me.EventServerChange, value)
        End AddHandler


        RemoveHandler(ByVal value As EventHandler)
            Me.Events.RemoveHandler(Me.EventServerChange, value)
        End RemoveHandler


        RaiseEvent(ByVal sender As Object, ByVal e As System.EventArgs)
            CType(Me.Events(Me.EventServerChange), EventHandler).Invoke(sender, e)
        End RaiseEvent
    End Event
End Class