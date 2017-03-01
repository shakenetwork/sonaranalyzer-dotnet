using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Tests.Diagnostics
{
    class Foo : ICollection<string> { }
    class Bar : ReadOnlyCollection<string> { }

    class Program
    {
        public readonly string[] strings; // Noncompliant
        protected readonly bool[] bools; // Noncompliant

        private readonly int[] ints; // Compliant
        internal readonly float[] floats; // Compliant

        public readonly Array array; // Noncompliant

        public readonly ICollection<string> iCollectionString; // Noncompliant
        public readonly IList<string> iListString; // Noncompliant
        public readonly List<string> listString; // Noncompliant
        public readonly LinkedList<string> linkedListString; // Noncompliant
        public readonly SortedList<string, string> sortedListString; // Noncompliant
        public readonly ObservableCollection<string> observableCollectionString; // Noncompliant

        public readonly ReadOnlyCollection<string> readonlyCollectionString; // Compliant
        public readonly ReadOnlyDictionary<string, string> readonlyDictionaryStrings; // Compliant
        public readonly IReadOnlyList<string> iReadonlyListString; // Compliant
        public readonly IReadOnlyCollection<string> iReadonlyCollectionString; // Compliant
        public readonly IReadOnlyDictionary<string, string> iReadonlyDictionaryStrings; // Compliant

        public string[] notReadonlyStrings; // Compliant

        public readonly Foo foo; // Noncompliant
        public readonly Bar bar; // Compliant
    }
}