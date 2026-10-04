namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// 32-bit FNV-1a. Used for state checksums.
    /// </summary>
    public static class Fnv1a
    {
        private const uint OffsetBasis = 2166136261;
        private const uint Prime = 16777619;

        public static uint Hash(byte[] data, int offset, int count)
        {
            uint hash = OffsetBasis;
            int end = offset + count;
            unchecked
            {
                for (int i = offset; i < end; i++)
                {
                    hash ^= data[i];
                    hash *= Prime;
                }
            }
            return hash;
        }
    }
}
