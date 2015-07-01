using System;
using System.Collections.Generic;

namespace Tests.Diagnostics
{
    public class StringUtils // Noncompliant
    {
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
        public static string Prop { get; set; }
    }

    public sealed class StringUtils22 //Noncompliant
    {
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
        public static string Prop { get; set; }
    }

    public class StringUtilsAsBase 
    {
        public StringUtilsAsBase() //Noncompliant, should be protected
        { }
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
        public static string Prop { get; set; }
    }


    public class BaseClass //Compliant, has no methods at all
    { }

    public class StringUtilsDerived : BaseClass
    {
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
        public static string Prop { get; set; }
    }

    public interface IInterface
    { }
    public class StringUtilsIf : IInterface
    {
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
        public static string Prop { get; set; }
    }

    public static class StringUtils2
    {
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
    }

    public class StringUtils3
    {
        protected StringUtils3()
        {
        }
        public static string Concatenate(string s1, string s2)
        {
            return s1 + s2;
        }
    }
}
