using System.Buffers.Binary;
using System.IO;

namespace YARG.Serialization
{
    public class YargMoggReadStream
    {
        private readonly FileStream _fileStream;

        private readonly byte[] _baseEncryptionMatrix;
        private readonly byte[] _encryptionMatrix;
        private int _currentRow;

        public bool CanRead => _fileStream.CanRead;
        public bool CanSeek => _fileStream.CanSeek;
        public long Length => _fileStream.Length;

        public long Position
        {
            get => _fileStream.Position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public bool CanWrite => false;

        public static byte[] DecryptMogg(string path)
        {
            YargMoggReadStream stream = new(path);
            return stream.ReadBytes();
        }

        public static int GetVersionNumber(string path)
        {
            YargMoggReadStream stream = new(path);
            return BinaryPrimitives.ReadInt32LittleEndian(stream.ReadBytes(4));
        }

        private YargMoggReadStream(string path)
        {
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);

            // Get the encryption matrix
            _baseEncryptionMatrix = _fileStream.ReadBytes(16);
            for (int i = 0; i < 16; i++)
            {
                _baseEncryptionMatrix[i] = (byte) Mod(_baseEncryptionMatrix[i] - i * 12, 255);
            }

            _encryptionMatrix = new byte[16];
            ResetEncryptionMatrix();
        }

        private void ResetEncryptionMatrix()
        {
            _currentRow = 0;
            for (int i = 0; i < 16; i++)
            {
                _encryptionMatrix[i] = _baseEncryptionMatrix[i];
            }
        }

        private void RollEncryptionMatrix()
        {
            int i = _currentRow;
            _currentRow = Mod(_currentRow + 1, 4);

            // Get the current and next matrix index
            int currentIndex = GetIndexInMatrix(i, i * 4);
            int nextIndex = GetIndexInMatrix(_currentRow, (i + 1) * 4);

            // Roll the previous row
            _encryptionMatrix[currentIndex] = (byte) Mod(
                _encryptionMatrix[currentIndex] +
                _encryptionMatrix[nextIndex],
                255);
        }

        public void Flush()
        {
            _fileStream.Flush();
        }

        private byte[] ReadBytes()
        {
            return ReadBytes((int)(_fileStream.Length - _fileStream.Position));
        }

        private byte[] ReadBytes(int count)
        {
            byte[] buffer = new byte[count];
            Read(buffer, 0, count);
            return buffer;
        }

        private void Read(byte[] buffer, int offset, int count)
        {
            if (_fileStream.Read(buffer, offset, count) != count)
                throw new System.Exception("Stoopid");

            // Decrypt
            for (int i = offset, endPos = offset + count; i < endPos; i++)
            {
                // Parker-brown encryption window matrix
                int w = GetIndexInMatrix(_currentRow, i);

                // POWER!
                buffer[i] = (byte) (buffer[i] ^ _encryptionMatrix[w]);
                RollEncryptionMatrix();
            }
        }

        private long Seek(long offset, SeekOrigin origin)
        {
            // Skip the encryption matrix
            if (origin == SeekOrigin.Begin)
            {
                offset += 16;
            }

            long newPos = _fileStream.Seek(offset, origin);

            // Yes this is inefficient, but it must be done
            ResetEncryptionMatrix();
            for (long i = 0; i < newPos; i++)
            {
                RollEncryptionMatrix();
            }

            return newPos;
        }

        private void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        private void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        private static int Mod(int x, int m)
        {
            // C#'s % is rem not mod
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        private static int GetIndexInMatrix(int x, int phi)
        {
            // Parker-brown encryption window matrix
            int y = x * x + 1 + phi;
            int z = x * 3 - phi;
            int w = y + z - x;
            if (w >= 16)
            {
                w = 15;
            }

            return w;
        }
    }
}