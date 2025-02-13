namespace LibTSforge.PhysicalStore
{
    using System;
    using System.Collections.Generic;

    public class PSBlock
    {
        public BlockType Type;
        public uint Flags;
        public uint Unknown = 0;
        public byte[] Key;
        public string KeyAsStr
        {
            get
            {
                return Utils.DecodeString(Key);
            }
            set
            {
                Key = Utils.EncodeString(value);
            }
        }
        public byte[] Value;
        public string ValueAsStr
        {
            get
            {
                return Utils.DecodeString(Value);
            }
            set
            {
                Value = Utils.EncodeString(value);
            }
        }
        public uint ValueAsInt
        {
            get
            {
                return BitConverter.ToUInt32(Value, 0);
            }
            set
            {
                Value = BitConverter.GetBytes(value);
            }
        }
        public byte[] Data;
        public string DataAsStr
        {
            get
            {
                return Utils.DecodeString(Data);
            }
            set
            {
                Data = Utils.EncodeString(value);
            }
        }
        public uint DataAsInt
        {
            get
            {
                return BitConverter.ToUInt32(Data, 0);
            }
            set
            {
                Data = BitConverter.GetBytes(value);
            }
        }
    }

    public interface IPhysicalStore : IDisposable
    {
        PSBlock GetBlock(string key, string value);
        PSBlock GetBlock(string key, uint value);
        void AddBlock(PSBlock block);
        void AddBlocks(IEnumerable<PSBlock> blocks);
        void SetBlock(string key, string value, byte[] data);
        void SetBlock(string key, string value, string data);
        void SetBlock(string key, string value, uint data);
        void SetBlock(string key, uint value, byte[] data);
        void SetBlock(string key, uint value, string data);
        void SetBlock(string key, uint value, uint data);
        void DeleteBlock(string key, string value);
        void DeleteBlock(string key, uint value);
        byte[] Serialize();
        void Deserialize(byte[] data);
        byte[] ReadRaw();
        void WriteRaw(byte[] data);
        IEnumerable<PSBlock> FindBlocks(string valueSearch);
        IEnumerable<PSBlock> FindBlocks(uint valueSearch);
    }
}
