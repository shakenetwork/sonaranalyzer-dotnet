using System;

namespace SonarQube.CSharp.CodeAnalysis.SonarQube.Settings.Sqale
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SqaleSubCharacteristicAttribute : Attribute
    {
        public SqaleSubCharacteristic SubCharacteristic { get; set; }

        public SqaleSubCharacteristicAttribute(SqaleSubCharacteristic subCharacteristic)
        {
            SubCharacteristic = subCharacteristic;
        }
    }
}