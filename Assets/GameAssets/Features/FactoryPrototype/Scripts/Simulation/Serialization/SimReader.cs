using System;

namespace Friendslop.Features.FactoryPrototype.Simulation
{
    /// <summary>
    /// Bounds-checked little-endian reader over a byte range. Throws on truncated data.
    /// </summary>
    public sealed class SimReader
    {
        private readonly byte[] _buffer;
        private readonly int _end;
        private int _position;

        public SimReader(byte[] buffer) : this(buffer, 0, buffer.Length)
        {
        }

        public SimReader(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            _buffer = buffer;
            _position = offset;
            _end = offset + count;
        }

        public int Remaining => _end - _position;

        public byte ReadByte()
        {
            Require(1);
            return _buffer[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUShort()
        {
            Require(2);
            int value = _buffer[_position] | (_buffer[_position + 1] << 8);
            _position += 2;
            return (ushort)value;
        }

        public int ReadInt()
        {
            Require(4);
            int value = _buffer[_position]
                | (_buffer[_position + 1] << 8)
                | (_buffer[_position + 2] << 16)
                | (_buffer[_position + 3] << 24);
            _position += 4;
            return value;
        }

        public uint ReadUInt() => unchecked((uint)ReadInt());

        public long ReadLong()
        {
            uint low = ReadUInt();
            int high = ReadInt();
            return ((long)high << 32) | low;
        }

        public Int3 ReadInt3()
        {
            int x = ReadInt();
            int y = ReadInt();
            int z = ReadInt();
            return new Int3(x, y, z);
        }

        private void Require(int count)
        {
            if (_position + count > _end)
                throw new FormatException("Simulation data is truncated.");
        }
    }
}
