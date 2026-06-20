namespace EtlKit.TestShared
{
    public static class TheoryDataExtensions
    {
        public static TheoryData<T> Concat<T>(this TheoryData<T> first, TheoryData<T> second)
        {
            var result = new TheoryData<T>();

            foreach (var item in first)
            {
                result.Add(item);
            }

            foreach (var item in second)
            {
                result.Add(item);
            }

            return result;
        }
    }
}
