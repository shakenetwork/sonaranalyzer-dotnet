using System;

namespace SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale
{
    [AttributeUsage(AttributeTargets.Class)]
    public abstract class SqaleRemediationAttribute : Attribute
    {
    }
}