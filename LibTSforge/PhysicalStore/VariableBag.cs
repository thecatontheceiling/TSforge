namespace LibTSforge.PhysicalStore
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public enum CRCBlockType : uint
    {
        UINT = 1 << 0,
        STRING = 1 << 1,
        BINARY = 1 << 2
    }

    public class CRCBlock
    {
        public CRCBlockType DataType;
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

        public void Encode(BinaryWriter writer)
        {
            uint crc = CRC();
            writer.Write(crc);
            writer.Write((uint)DataType);
            writer.Write(Key.Length);
            writer.Write(Value.Length);

            writer.Write(Key);
            writer.Align(8);

            writer.Write(Value);
            writer.Align(8);
        }

        public static CRCBlock Decode(BinaryReader reader)
        {
            uint crc = reader.ReadUInt32();
            uint type = reader.ReadUInt32();
            uint lenName = reader.ReadUInt32();
            uint lenVal = reader.ReadUInt32();

            byte[] key = reader.ReadBytes((int)lenName);
            reader.Align(8);

            byte[] value = reader.ReadBytes((int)lenVal);
            reader.Align(8);

            CRCBlock block = new CRCBlock
            {
                DataType = (CRCBlockType)type,
                Key = key,
                Value = value,
            };

            if (block.CRC() != crc)
            {
                throw new InvalidDataException("Invalid CRC in variable bag.");
            }

            return block;
        }

        public uint CRC()
        {
            BinaryWriter wtemp = new BinaryWriter(new MemoryStream());
            wtemp.Write(0);
            wtemp.Write((uint)DataType);
            wtemp.Write(Key.Length);
            wtemp.Write(Value.Length);
            wtemp.Write(Key);
            wtemp.Write(Value);
            return Utils.CRC32(wtemp.GetBytes());
        }
    }

    public class VariableBag
    {
        public List<CRCBlock> Blocks = new List<CRCBlock>();

        public void Deserialize(byte[] data)
        {
            int len = data.Length;

            BinaryReader reader = new BinaryReader(new MemoryStream(data));

            while (reader.BaseStream.Position < len - 0x10)
            {
                Blocks.Add(CRCBlock.Decode(reader));
            }
        }

        public byte[] Serialize()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());

            foreach (CRCBlock block in Blocks)
            {
                block.Encode(writer);
            }

            return writer.GetBytes();
        }

        public CRCBlock GetBlock(string key)
        {
            foreach (CRCBlock block in Blocks)
            {
                if (block.KeyAsStr == key)
                {
                    return block;
                }
            }

            return null;
        }

        public void SetBlock(string key, byte[] value)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                CRCBlock block = Blocks[i];

                if (block.KeyAsStr == key)
                {
                    block.Value = value;
                    Blocks[i] = block;
                    break;
                }
            }
        }

        public void DeleteBlock(string key)
        {
            foreach (CRCBlock block in Blocks)
            {
                if (block.KeyAsStr == key)
                {
                    Blocks.Remove(block);
                    return;
                }
            }
        }

        public VariableBag(byte[] data)
        {
            Deserialize(data);
        }

        public VariableBag()
        {

        }
    }
}
