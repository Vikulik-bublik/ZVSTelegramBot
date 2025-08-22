using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Helpers
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> GetBatchByNumber<T>(this IEnumerable<T> source, int batchSize, int batchNumber)
        {
            if (source == null)
            throw new ArgumentNullException(nameof(source));
            if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Размер пачки должен быть положительным числом");
            if (batchNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(batchNumber), "Номер пачки не может быть отрицательным");
            return source.Skip(batchNumber * batchSize).Take(batchSize);
        }
    }
}
