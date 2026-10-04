using System;
using Friendslop.Features.FactoryPrototype.Simulation;
using NUnit.Framework;

namespace Friendslop.Features.FactoryPrototype.Tests
{
    public class SerializationTests
    {
        [Test]
        public void WriterReader_RoundTripsPrimitives()
        {
            var writer = new SimWriter(16);
            writer.WriteByte(200);
            writer.WriteBool(true);
            writer.WriteUShort(65000);
            writer.WriteInt(-123456789);
            writer.WriteUInt(4000000000);
            writer.WriteLong(-9876543210123L);
            writer.WriteInt3(new Int3(-32, 0, 31));

            var reader = new SimReader(writer.ToArray());
            Assert.AreEqual(200, reader.ReadByte());
            Assert.IsTrue(reader.ReadBool());
            Assert.AreEqual(65000, reader.ReadUShort());
            Assert.AreEqual(-123456789, reader.ReadInt());
            Assert.AreEqual(4000000000u, reader.ReadUInt());
            Assert.AreEqual(-9876543210123L, reader.ReadLong());
            Assert.AreEqual(new Int3(-32, 0, 31), reader.ReadInt3());
            Assert.AreEqual(0, reader.Remaining);
        }

        [Test]
        public void Reader_ThrowsOnTruncatedData()
        {
            var reader = new SimReader(new byte[] { 1, 2, 3 });
            Assert.Throws<FormatException>(() => reader.ReadInt());
        }

        [Test]
        public void Reader_RespectsOffsetAndCount()
        {
            var reader = new SimReader(new byte[] { 9, 9, 7, 0, 9 }, 2, 2);
            Assert.AreEqual(7, reader.ReadUShort());
            Assert.AreEqual(0, reader.Remaining);
        }

        [Test]
        public void SimCommand_RoundTrip()
        {
            SimCommand place = SimCommand.Place(PrototypeContent.Mixer, new Int3(3, 0, -4), 3);
            SimCommand remove = SimCommand.Remove(new Int3(-1, 0, 2));

            var writer = new SimWriter();
            place.Write(writer);
            remove.Write(writer);

            var reader = new SimReader(writer.ToArray());
            Assert.AreEqual(place, SimCommand.Read(reader));
            Assert.AreEqual(remove, SimCommand.Read(reader));
        }

        [Test]
        public void SimCommand_RejectsUnknownType()
        {
            var reader = new SimReader(new byte[] { 99, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            Assert.Throws<FormatException>(() => SimCommand.Read(reader));
        }

        [Test]
        public void Fnv1a_KnownValuesAndRange()
        {
            Assert.AreEqual(2166136261u, Fnv1a.Hash(new byte[0], 0, 0));
            // Reference value for "a".
            Assert.AreEqual(0xE40C292Cu, Fnv1a.Hash(new byte[] { (byte)'a' }, 0, 1));
            Assert.AreEqual(Fnv1a.Hash(new byte[] { (byte)'a' }, 0, 1), Fnv1a.Hash(new byte[] { 0, (byte)'a' }, 1, 1));
        }
    }
}
