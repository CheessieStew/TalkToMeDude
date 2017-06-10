using System;
using System.Collections.Generic;

namespace FirstTry.Utils
{
    internal static class EnumerableExtensions
    {

        public static Tuple<T, IEnumerable<T>> HeadAndTail<T>(this IEnumerable<T> e)
        {
            var en = e.GetEnumerator();
            if (!en.MoveNext())
                return null;
            var res = new Tuple<T, IEnumerable<T>>(en.Current, Tail(en));
            en.Dispose();
            return res;
        }

        private static IEnumerable<T> Tail<T>(IEnumerator<T> en)
        {
            while (en.MoveNext())
            {
                yield return en.Current;
            }
        }
    }
}