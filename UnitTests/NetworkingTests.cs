using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared.MVVM.Model.Cryptography;
using Shared.MVVM.Model.Networking;
using System.Net;
using UnitTests.Mocks;

namespace UnitTests
{
    [TestClass]
    public class NetworkingTests
    {
        [TestMethod]
        public void IPAddress_HostToNetworkOrder_Ushort_ShouldBeRestorable()
        {
            // Arrange
            ushort expected = 0x9B40;

            // Act
            short inNetworkOrder = IPAddress.HostToNetworkOrder((short)expected);
            ushort actual = (ushort)IPAddress.NetworkToHostOrder(inNetworkOrder);

            // Assert
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void PacketBuilder_Build_UnencryptedUnauthenticatedPacket_ShouldReturnExpectedPacket()
        {
            // Arrange
            PacketBuilder pb = new PacketBuilder();
            byte[] expPacket = new byte[] {
                0, 0, 0, 2 + 4,
                0x12, 0x34,
                0x00, 0x0A, 0xBC, 0xDE
            };
            const int expLengthWoPrefix = 2 + 4;
            const int expPrefixValue = expLengthWoPrefix;

            // Act
            pb.Append(0x1234, 2);
            pb.Append(0xABCDE, 4);
            byte[] actPacket = pb.Build();
            int actLengthWoPrefix = actPacket.Length - PacketSendBuffer.PACKET_PREFIX_SIZE;
            int actPrefixValue = IPAddress.NetworkToHostOrder(
                BitConverter.ToInt32(actPacket, 0));

            // Assert
            Console.WriteLine($"expected: {expPacket.ToHexString()}");
            Console.WriteLine($"actual: {actPacket.ToHexString()}");

            Assert.AreEqual(expLengthWoPrefix, actLengthWoPrefix);
            Assert.AreEqual(expPrefixValue, actPrefixValue);
            expPacket.BytesEqual(actPacket);
        }

        [TestMethod]
        public void PacketBuilder_Build_UnencryptedAuthenticatedPacket_ShouldReturnExpectedPacket()
        {
            // Arrange
            PrivateKey privateKey = PrivateKey.Random();
            PublicKey publicKey = privateKey.ToPublicKey();
            PacketBuilder pb = new PacketBuilder();

            // Act
            pb.Append(0x1234, 2);
            pb.Append(0xABCDE, 4);
            pb.Sign(privateKey);
            pb.Prepend(0x03, 1);
            byte[] built = pb.Build();

            // Assert
            byte[] actSignatureLength = built.Slice(5, 2);
            ushort actSignatureLengthValue = (ushort)IPAddress.NetworkToHostOrder(
                BitConverter.ToInt16(actSignatureLength, 0));

            // byte[] expPrefix = new byte[] { 0x00, 0x00, 0x01, 0x09 };
            byte[] expPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(
                1 // kod operacji
                + sizeof(ushort) // długość sygnatury
                + (int)actSignatureLengthValue // wartość długości sygnatury
                + 6)); // payload
            byte[] actPrefix = built.Slice(0, 4);
            expPrefix.BytesEqual(actPrefix);

            byte[] expOpCode = new byte[] { 0x03 };
            byte[] actOpCode = built.Slice(4, 1);
            expOpCode.BytesEqual(actOpCode);

            byte[] expPayload = new byte[] { 0x12, 0x34, 0x00, 0x0A, 0xBC, 0xDE };
            byte[] actPayload = built.Slice(7 + actSignatureLengthValue, 6);
            expPayload.BytesEqual(actPayload);

            byte[] actSignature = built.Slice(7, actSignatureLengthValue);
            Assert.IsTrue(Rsa.Verify(publicKey, actPayload, actSignature));

            Console.WriteLine($"prefix: size {actPrefix.Length}; {actPrefix.ToHexString()}");
            Console.WriteLine($"op code: size {actOpCode.Length}; {actOpCode.ToHexString()}");
            Console.WriteLine($"signature length: size {actSignatureLength.Length}; " +
                $"{actSignatureLength.ToHexString()}");
            Console.WriteLine($"signature: size {actSignature.Length}; {actSignature.ToHexString()}");
            Console.WriteLine($"payload: size {actPayload.Length}; {actPayload.ToHexString()}");
        }

        [TestMethod]
        public void PacketBuilder_Build_UnencryptedAuthenticatedPacket_ShouldReturnExpectedPacket_AssertUsingPacketReader()
        {
            // Arrange
            PrivateKey privateKey = PrivateKey.Random();
            PublicKey publicKey = privateKey.ToPublicKey();
            PacketBuilder pb = new PacketBuilder();

            // Act
            pb.Append(0x1234, 2);
            pb.Append(0xABCDE, 4);
            pb.Sign(privateKey);
            pb.Prepend(0x03, 1);
            byte[] built = pb.Build();

            // Assert
            PacketReader pr = new PacketReader(built);
            int actPrefix = pr.ReadInt32();

            int actOpCode = pr.ReadUInt8();
            Assert.AreEqual(0x03, actOpCode);

            Assert.IsTrue(pr.VerifySignature(publicKey));

            byte[] actPayload = pr.ReadBytes(6);
            new byte[] { 0x12, 0x34, 0x00, 0x0A, 0xBC, 0xDE }.BytesEqual(actPayload);

            Console.WriteLine($"prefix: {actPrefix}");
            Console.WriteLine($"op code: {actOpCode}");
            Console.WriteLine($"payload: {actPayload.ToHexString()}");
        }

        [TestMethod]
        public void PacketBuilder_Build_EncryptedUnauthenticatedPacket_ShouldReturnExpectedPacket()
        {
            // Arrange
            PrivateKey privateKey = PrivateKey.Random();
            PublicKey publicKey = privateKey.ToPublicKey();
            PacketBuilder pb = new PacketBuilder();

            // Act
            pb.Append(0x1234, 2);
            pb.Append(0xABCDE, 4);
            pb.Encrypt(publicKey);
            pb.Prepend(0x03, 1);
            byte[] built = pb.Build();

            // Assert
            PacketReader pr = new PacketReader(built);
            int actPrefix = pr.ReadInt32();

            int actOpCode = pr.ReadUInt8();
            Assert.AreEqual(0x03, actOpCode);

            pr.Decrypt(privateKey);

            byte[] actPayload = pr.ReadBytes(6);
            new byte[] { 0x12, 0x34, 0x00, 0x0A, 0xBC, 0xDE }.BytesEqual(actPayload);

            Console.WriteLine($"prefix: {actPrefix}");
            Console.WriteLine($"op code: {actOpCode}");
            Console.WriteLine($"payload: {actPayload.ToHexString()}");
        }

        [TestMethod]
        public void PacketBuilder_Build_EncryptedAuthenticatedPacket_ShouldReturnExpectedPacket()
        {
            // Arrange
            PrivateKey receiverPrivateKey = PrivateKey.Random();
            PublicKey receiverPublicKey = receiverPrivateKey.ToPublicKey();

            PrivateKey senderPrivateKey = PrivateKey.Random();
            PublicKey senderPublicKey = senderPrivateKey.ToPublicKey();
            PacketBuilder pb = new PacketBuilder();

            // Act
            pb.Append(0x1234, 2);
            pb.Append(0xABCDE, 4);
            pb.Sign(senderPrivateKey);
            pb.Encrypt(receiverPublicKey);
            pb.Prepend(0x03, 1);
            byte[] built = pb.Build();

            // Assert
            PacketReader pr = new PacketReader(built);
            int actPrefix = pr.ReadInt32();

            int actOpCode = pr.ReadUInt8();
            Assert.AreEqual(0x03, actOpCode);

            pr.Decrypt(receiverPrivateKey);

            Assert.IsTrue(pr.VerifySignature(senderPublicKey));

            byte[] actPayload = pr.ReadBytes(6);
            new byte[] { 0x12, 0x34, 0x00, 0x0A, 0xBC, 0xDE }.BytesEqual(actPayload);

            Console.WriteLine($"prefix: {actPrefix}");
            Console.WriteLine($"op code: {actOpCode}");
            Console.WriteLine($"payload: {actPayload.ToHexString()}");
        }

        [TestMethod]
        public void PacketBuilder_Sign_WithoutData_ShouldOnlyPrependSignature()
        {
            // Arrange
            PrivateKey privateKey = PrivateKey.Random();
            PublicKey publicKey = privateKey.ToPublicKey(); 
            PacketBuilder pb = new PacketBuilder();

            // Act
            pb.Sign(privateKey);
            byte[] built = pb.Build();

            // Assert
            PacketReader pr = new PacketReader(built);
            int actPrefix = pr.ReadInt32();

            Assert.IsTrue(pr.VerifySignature(publicKey));

            pr = new PacketReader(built);
            pr.ReadInt32();
            int actSignatureLength = pr.ReadUInt16();
            byte[] actSignatureValue = pr.ReadBytes(actSignatureLength);

            Console.WriteLine($"prefix: {actPrefix}");
            Console.WriteLine($"signature length: {actSignatureLength}");
            Console.WriteLine($"signature value: {actSignatureValue.ToHexString()}");
        }

        [TestMethod]
        public void PacketReceiveBuffer_ReceiveUntilCompletedOrInterrupted_WhenReceived2OrdinaryPackets_ShouldReturnThem()
        {
            // Arrange
            var packetReceiveBuffer = new PacketReceiveBuffer();

            int[] returnedByteCounts = new int[] { 4, 3, 5, 2 };
            byte[] byteStream = new byte[]
            {
                0, 0, 0, 4, 1, 2, 3, 4, // Pierwszy pakiet
                0, 0, 0, 2, 5, 6 // Drugi pakiet
            };
            var socketMock = new ReceiveSocketMock(returnedByteCounts, byteStream);

            // Act
            byte[]? actualPacket0 = packetReceiveBuffer.ReceiveUntilCompletedOrInterrupted(
                socketMock, CancellationToken.None);
            byte[]? actualPacket1 = packetReceiveBuffer.ReceiveUntilCompletedOrInterrupted(
                socketMock, CancellationToken.None);

            // Assert
            Console.WriteLine(actualPacket0!.ToHexString());
            Console.WriteLine(actualPacket1!.ToHexString());

            byteStream.Slice(4, 4).BytesEqual(actualPacket0); // Pierwszy pakiet
            byteStream.Slice(12, 2).BytesEqual(actualPacket1); // Drugi pakiet
        }

        [TestMethod]
        public void PacketReceiveBuffer_ReceiveUntilCompletedOrInterrupted_WhenReceived0Bytes_ShouldReturnNull()
        {
            // Arrange
            var packetReceiveBuffer = new PacketReceiveBuffer();

            int[] returnedByteCounts = new int[] { };
            byte[] byteStream = new byte[] { };
            var socketMock = new ReceiveSocketMock(returnedByteCounts, byteStream);

            // Act
            byte[]? actualPacket0 = packetReceiveBuffer.ReceiveUntilCompletedOrInterrupted(
                socketMock, CancellationToken.None);
            byte[]? actualPacket1 = packetReceiveBuffer.ReceiveUntilCompletedOrInterrupted(
                socketMock, CancellationToken.None);

            // Assert
            Assert.IsNull(actualPacket0);
            Assert.IsNull(actualPacket1);
        }

        [TestMethod]
        public void PacketReceiveBuffer_ReceiveUntilCompletedOrInterrupted_WhenReceivedKeepAlive_ShouldReturn0LengthPacket()
        {
            // Arrange
            var packetReceiveBuffer = new PacketReceiveBuffer();

            int[] returnedByteCounts = new int[] { 1, 2, 1 };
            byte[] byteStream = new byte[] { 0, 0, 0, 0 }; // Keep alive
            var socketMock = new ReceiveSocketMock(returnedByteCounts, byteStream);

            // Act
            byte[]? actualPacket = packetReceiveBuffer.ReceiveUntilCompletedOrInterrupted(
                socketMock, CancellationToken.None);

            // Assert
            Assert.AreEqual(0, actualPacket!.Length);
        }

        [TestMethod]
        public void PacketReceiveBuffer_ReceiveUntilCompletedOrInterrupted_WhenReceivedIncompletePacket_ShouldReturnNull()
        {
            // Arrange
            var packetReceiveBuffer = new PacketReceiveBuffer();

            int[] returnedByteCounts = new int[] { 1, 1, 1 };
            byte[] byteStream = new byte[] { 1, 0, 0 }; // Niepełny pakiet
            var socketMock = new ReceiveSocketMock(returnedByteCounts, byteStream);

            // Act
            byte[]? actualPacket = packetReceiveBuffer.ReceiveUntilCompletedOrInterrupted(
                socketMock, CancellationToken.None);

            // Assert
            Assert.IsNull(actualPacket);
        }
    }
}
