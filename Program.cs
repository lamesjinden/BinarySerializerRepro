using BinarySerialization;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace BinarySerializerRepro
{

    /// <summary>
    /// Wraps a stream to look like a NetworkStream
    /// </summary>
    public class LooksLikeANetworkStream : Stream
    {

        private Stream UnderlyingStream { get; }

        public LooksLikeANetworkStream(Stream source)
        {
            UnderlyingStream = source;
        }

        public override void Flush()
        {
            UnderlyingStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return UnderlyingStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            UnderlyingStream.Write(buffer, offset, count);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length
        {
            get { throw new NotSupportedException();}
        }
        public override long Position
        {
            get { return UnderlyingStream.Position; }
            set { UnderlyingStream.Position = value; }
        }
    }

    public class Chunk { }

    public class ImageHeaderChunk : Chunk { }

    public class PaletteChunk : Chunk { }

    public class ImageDataChunk : Chunk { }

    public class TestChunk : Chunk
    {
        [FieldOrder(0)]
        public CustomSerializable[] Customs;
    }

    public class CustomSerializable : IBinarySerializable
    {

        [BinarySerialization.Ignore]
        public byte Value;

        public void Serialize(Stream stream, Endianness endianness, BinarySerializationContext serializationContext)
        {
            stream.WriteByte(Value);
        }

        public void Deserialize(Stream stream, Endianness endianness, BinarySerializationContext serializationContext)
        {
            var readByte = stream.ReadByte();
            if (readByte == -1) throw new EndOfStreamException();
            Value = Convert.ToByte(readByte);
        }
    }

    public class ChunkContainer
    {
        [FieldOrder(0)]
        public int Length { get; set; }

        [FieldOrder(1)]
        [FieldLength(4)]
        public string ChunkType { get; set; }

        [FieldOrder(2)]
        [FieldLength("Length")]
        [Subtype("ChunkType", "IHDR", typeof(ImageHeaderChunk))]
        [Subtype("ChunkType", "PLTE", typeof(PaletteChunk))]
        [Subtype("ChunkType", "IDAT", typeof(ImageDataChunk))]
        [Subtype("ChunkType", "TEST", typeof(TestChunk))]
        public Chunk Chunk { get; set; }

        [FieldOrder(3)]
        public int Crc { get; set; }
    }

    public class Program
    {
        static void Main(string[] args)
        {

            var source = new ChunkContainer
            {
                ChunkType = "TEST",
                Chunk = new TestChunk
                {
                    Customs = new[]
                    {
                        new CustomSerializable
                        {
                            Value = 1
                        },
                        new CustomSerializable
                        {
                            Value = 2
                        }
                    }
                }
            };

            var serializer = new BinarySerializer();
            var outputStream = new MemoryStream();
            serializer.Serialize(outputStream, source);

            outputStream.Seek(0, SeekOrigin.Begin);
            var inputStream = new LooksLikeANetworkStream(outputStream); //non-seekable stream
            //var inputStream = outputStream; //successful

            var roundtrip = serializer.Deserialize<ChunkContainer>(inputStream);
            Assert.That(roundtrip.Chunk, Is.InstanceOf<TestChunk>());

            var sourceChunk = (TestChunk) source.Chunk;
            var testChunk = (TestChunk) roundtrip.Chunk;
            Assert.That(testChunk.Customs.Length, Is.EqualTo(sourceChunk.Customs.Length));
            Assert.That(testChunk.Customs.ElementAt(0).Value, Is.EqualTo(sourceChunk.Customs.ElementAt(0).Value));
            Assert.That(testChunk.Customs.ElementAt(1).Value, Is.EqualTo(sourceChunk.Customs.ElementAt(1).Value));
        }
    }
}
