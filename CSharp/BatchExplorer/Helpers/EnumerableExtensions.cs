using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.BatchExplorer.Helpers
{
    internal static class EnumerableExtensions
    {
        internal static IEnumerable<T> SelectMany<T>(this IEnumerable source, Func<object, int, IEnumerable<T>> selector)
        {
            int index = 0;
            foreach (var item in source)
            {
                foreach (var result in selector(item, index))
                {
                    yield return result;
                }
                ++index;
            }
        }
    }
}
