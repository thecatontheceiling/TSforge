namespace LibTSforge.PhysicalStore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using LibTSforge.Crypto;

    public class Win7Block
    {
        public BlockType Type;
        public uint Flags;
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

        internal void Encode(BinaryWriter writer)
        {
            writer.Write((uint)Type);
            writer.Write(Flags);
            writer.Write(Key.Length);
            writer.Write(Value.Length);
            writer.Write(Data.Length);
            writer.Write(Key);
            writer.Write(Value);
            writer.Write(Data);
        }

        internal static Win7Block Decode(BinaryReader reader)
        {
            uint type = reader.ReadUInt32();
            uint flags = reader.ReadUInt32();

            int keyLen = reader.ReadInt32();
            int valueLen = reader.ReadInt32();
            int dataLen = reader.ReadInt32();

            byte[] key = reader.ReadBytes(keyLen);
            byte[] value = reader.ReadBytes(valueLen);
            byte[] data = reader.ReadBytes(dataLen);
            return new Win7Block
            {
                Type = (BlockType)type,
                Flags = flags,
                Key = key,
                Value = value,
                Data = data,
            };
        }
    }

    public sealed class PhysicalStoreWin7 : IPhysicalStore
    {
        private byte[] PreHeaderBytes = new byte[] { };
        private readonly List<Win7Block> Blocks = new List<Win7Block>();
        private readonly FileStream TSPrimary;
        private readonly FileStream TSSecondary;
        private readonly bool Production;

        public byte[] Serialize()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            writer.Write(PreHeaderBytes);

            foreach (Win7Block block in Blocks)
            {
                block.Encode(writer);
                writer.Align(4);
            }

            return writer.GetBytes();
        }

        public void Deserialize(byte[] data)
        {
            int len = data.Length;

            BinaryReader reader = new BinaryReader(new MemoryStream(data));
            PreHeaderBytes = reader.ReadBytes(8);

            while (reader.BaseStream.Position < len - 0x14)
            {
                Blocks.Add(Win7Block.Decode(reader));
                reader.Align(4);
            }
        }

        public void AddBlock(PSBlock block)
        {
            Blocks.Add(new Win7Block
            {
                Type = block.Type,
                Flags = block.Flags,
                Key = block.Key,
                Value = block.Value,
                Data = block.Data
            });
        }

        public void AddBlocks(IEnumerable<PSBlock> blocks)
        {
            foreach (PSBlock block in blocks)
            {
                AddBlock(block);
            }
        }

        public PSBlock GetBlock(string key, string value)
        {
            foreach (Win7Block block in Blocks)
            {
                if (block.KeyAsStr == key && block.ValueAsStr == value)
                {
                    return new PSBlock
                    {
                        Type = block.Type,
                        Flags = block.Flags,
                        Key = block.Key,
                        Value = block.Value,
                        Data = block.Data
                    };
                }
            }

            return null;
        }

        public PSBlock GetBlock(string key, uint value)
        {
            foreach (Win7Block block in Blocks)
            {
                if (block.KeyAsStr == key && block.ValueAsInt == value)
                {
                    return new PSBlock
                    {
                        Type = block.Type,
                        Flags = block.Flags,
                        Key = block.Key,
                        Value = block.Value,
                        Data = block.Data
                    };
                }
            }

            return null;
        }

        public void SetBlock(string key, string value, byte[] data)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                Win7Block block = Blocks[i];

                if (block.KeyAsStr == key && block.ValueAsStr == value)
                {
                    block.Data = data;
                    Blocks[i] = block;
                    break;
                }
            }
        }

        public void SetBlock(string key, uint value, byte[] data)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                Win7Block block = Blocks[i];

                if (block.KeyAsStr == key && block.ValueAsInt == value)
                {
                    block.Data = data;
                    Blocks[i] = block;
                    break;
                }
            }
        }

        public void SetBlock(string key, string value, string data)
        {
            SetBlock(key, value, Utils.EncodeString(data));
        }

        public void SetBlock(string key, string value, uint data)
        {
            SetBlock(key, value, BitConverter.GetBytes(data));
        }

        public void SetBlock(string key, uint value, string data)
        {
            SetBlock(key, value, Utils.EncodeString(data));
        }

        public void SetBlock(string key, uint value, uint data)
        {
            SetBlock(key, value, BitConverter.GetBytes(data));
        }

        public void DeleteBlock(string key, string value)
        {
            foreach (Win7Block block in Blocks)
            {
                if (block.KeyAsStr == key && block.ValueAsStr == value)
                {
                    Blocks.Remove(block);
                    return;
                }
            }
        }

        public void DeleteBlock(string key, uint value)
        {
            foreach (Win7Block block in Blocks)
            {
                if (block.KeyAsStr == key && block.ValueAsInt == value)
                {
                    Blocks.Remove(block);
                    return;
                }
            }
        }

        public PhysicalStoreWin7(string primaryPath, bool production)
        {
            TSPrimary = File.Open(primaryPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            TSSecondary = File.Open(primaryPath.Replace("-0.", "-1."), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Production = production;

            Deserialize(PhysStoreCrypto.DecryptPhysicalStore(TSPrimary.ReadAllBytes(), production));
            TSPrimary.Seek(0, SeekOrigin.Begin);
        }

        public void Dispose()
        {
            if (TSPrimary.CanWrite && TSSecondary.CanWrite)
            {
                byte[] data = PhysStoreCrypto.EncryptPhysicalStore(Serialize(), Production, PSVersion.Win7);

                TSPrimary.SetLength(data.LongLength);
                TSSecondary.SetLength(data.LongLength);

                TSPrimary.Seek(0, SeekOrigin.Begin);
                TSSecondary.Seek(0, SeekOrigin.Begin);

                TSPrimary.WriteAllBytes(data);
                TSSecondary.WriteAllBytes(data);

                TSPrimary.Close();
                TSSecondary.Close();
            }
        }

        public byte[] ReadRaw()
        {
            byte[] data = PhysStoreCrypto.DecryptPhysicalStore(TSPrimary.ReadAllBytes(), Production);
            TSPrimary.Seek(0, SeekOrigin.Begin);
            return data;
        }

        public void WriteRaw(byte[] data)
        {
            byte[] encrData = PhysStoreCrypto.EncryptPhysicalStore(data, Production, PSVersion.Win7);

            TSPrimary.SetLength(encrData.LongLength);
            TSSecondary.SetLength(encrData.LongLength);

            TSPrimary.Seek(0, SeekOrigin.Begin);
            TSSecondary.Seek(0, SeekOrigin.Begin);

            TSPrimary.WriteAllBytes(encrData);
            TSSecondary.WriteAllBytes(encrData);

            TSPrimary.Close();
            TSSecondary.Close();
        }

        public IEnumerable<PSBlock> FindBlocks(string valueSearch)
        {
            List<PSBlock> results = new List<PSBlock>();

            foreach (Win7Block block in Blocks)
            {
                if (block.ValueAsStr.Contains(valueSearch))
                {
                    results.Add(new PSBlock
                    {
                        Type = block.Type,
                        Flags = block.Flags,
                        Key = block.Key,
                        Value = block.Value,
                        Data = block.Data
                    });
                }
            }

            return results;
        }

        public IEnumerable<PSBlock> FindBlocks(uint valueSearch)
        {
            List<PSBlock> results = new List<PSBlock>();

            foreach (Win7Block block in Blocks)
            {
                if (block.ValueAsInt == valueSearch)
                {
                    results.Add(new PSBlock
                    {
                        Type = block.Type,
                        Flags = block.Flags,
                        Key = block.Key,
                        Value = block.Value,
                        Data = block.Data
                    });
                }
            }

            return results;
        }
    }
}
