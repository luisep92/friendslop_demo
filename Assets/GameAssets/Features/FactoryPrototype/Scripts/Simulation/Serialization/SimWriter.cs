using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Growable little-endian binary writer. Reuse with Clear() to avoid allocations.
    /// </summary>
    public sealed class SimWriter
    {
        private byte[] _buffer;
        private int _length;

        public SimWriter(int capacity = 256)
        {
            _buffer = new byte[Math.Max(16, capacity)];
        }

        /// <summary>Backing buffer. Valid bytes are [0, Length).</summary>
        public byte[] Buffer => _buffer;
        public int Length => _length;

        public void Clear() => _length = 0;

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUShort(ushort value)
        {
            Ensure(2);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
        }

        public void WriteInt(int value)
        {
            Ensure(4);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
            _buffer[_length++] = (byte)(value >> 16);
            _buffer[_length++] = (byte)(value >> 24);
        }

        public void WriteUInt(uint value) => WriteInt(unchecked((int)value));

        public void WriteLong(long value)
        {
            WriteInt(unchecked((int)value));
            WriteInt(unchecked((int)(value >> 32)));
        }

        public void WriteInt3(Int3 value)
        {
            WriteInt(value.X);
            WriteInt(value.Y);
            WriteInt(value.Z);
        }

        public byte[] ToArray()
        {
            var result = new byte[_length];
            Array.Copy(_buffer, result, _length);
            return result;
        }

        private void Ensure(int count)
        {
            if (_length + count > _buffer.Length)
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + count));
        }
    }
}
