using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    [System.ServiceModel.ServiceContract]
    interface IMyService1
    {
        [System.ServiceModel.OperationContract]
        int MyServiceMethod();
        int MyServiceMethod2();
    }

    [System.ServiceModel.ServiceContract]
    interface IMyService2 // Noncompliant
    {
        int MyServiceMethod();
    }

    class IMyService3 // Noncompliant
    {
        [System.ServiceModel.OperationContract]
        int MyServiceMethod() { }
    }
}
