namespace LibTSforge.PhysicalStore
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Crypto;

    public class ModernBlock
    {
        public BlockType Type;
        public uint Flags;
        public uint Unknown;
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

        public void Encode(BinaryWriter writer)
        {
            writer.Write((uint)Type);
            writer.Write(Flags);
            writer.Write((uint)Value.Length);
            writer.Write((uint)Data.Length);
            writer.Write(Unknown);
            writer.Write(Value);
            writer.Write(Data);
        }

        public static ModernBlock Decode(BinaryReader reader)
        {
            uint type = reader.ReadUInt32();
            uint flags = reader.ReadUInt32();

            uint valueLen = reader.ReadUInt32();
            uint dataLen = reader.ReadUInt32();
            uint unk3 = reader.ReadUInt32();

            byte[] value = reader.ReadBytes((int)valueLen);
            byte[] data = reader.ReadBytes((int)dataLen);

            return new ModernBlock
            {
                Type = (BlockType)type,
                Flags = flags,
                Unknown = unk3,
                Value = value,
                Data = data,
            };
        }
    }

    public sealed class PhysicalStoreModern : IPhysicalStore
    {
        private byte[] PreHeaderBytes = { };
        private readonly Dictionary<string, List<ModernBlock>> Data = new Dictionary<string, List<ModernBlock>>();
        private readonly FileStream TSFile;
        private readonly PSVersion Version;
        private readonly bool Production;

        public byte[] Serialize()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            writer.Write(PreHeaderBytes);
            writer.Write(Data.Keys.Count);

            foreach (string key in Data.Keys)
            {
                List<ModernBlock> blocks = Data[key];
                byte[] keyNameEnc = Utils.EncodeString(key);

                writer.Write(keyNameEnc.Length);
                writer.Write(keyNameEnc);
                writer.Write(blocks.Count);
                writer.Align(4);

                foreach (ModernBlock block in blocks)
                {
                    block.Encode(writer);
                    writer.Align(4);
                }
            }

            return writer.GetBytes();
        }

        public void Deserialize(byte[] data)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(data));
            PreHeaderBytes = reader.ReadBytes(8);

            while (reader.BaseStream.Position < data.Length - 0x4)
            {
                uint numKeys = reader.ReadUInt32();

                for (int i = 0; i < numKeys; i++)
                {
                    uint lenKeyName = reader.ReadUInt32();
                    string keyName = Utils.DecodeString(reader.ReadBytes((int)lenKeyName)); uint numValues = reader.ReadUInt32();

                    reader.Align(4);

                    Data[keyName] = new List<ModernBlock>();

                    for (int j = 0; j < numValues; j++)
                    {
                        Data[keyName].Add(ModernBlock.Decode(reader));
                        reader.Align(4);
                    }
                }
            }
        }

        public void AddBlock(PSBlock block)
        {
            if (!Data.ContainsKey(block.KeyAsStr))
            {
                Data[block.KeyAsStr] = new List<ModernBlock>();
            }

            Data[block.KeyAsStr].Add(new ModernBlock
            {
                Type = block.Type,
                Flags = block.Flags,
                Unknown = block.Unknown,
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
            List<ModernBlock> blocks = Data[key];

            foreach (ModernBlock block in blocks)
            {
                if (block.ValueAsStr == value)
                {
                    return new PSBlock
                    {
                        Type = block.Type,
                        Flags = block.Flags,
                        Key = Utils.EncodeString(key),
                        Value = block.Value,
                        Data = block.Data
                    };
                }
            }

            return null;
        }

        public PSBlock GetBlock(string key, uint value)
        {
            List<ModernBlock> blocks = Data[key];

            foreach (ModernBlock block in blocks)
            {
                if (block.ValueAsInt == value)
                {
                    return new PSBlock
                    {
                        Type = block.Type,
                        Flags = block.Flags,
                        Key = Utils.EncodeString(key),
                        Value = block.Value,
                        Data = block.Data
                    };
                }
            }

            return null;
        }

        public void SetBlock(string key, string value, byte[] data)
        {
            List<ModernBlock> blocks = Data[key];

            for (int i = 0; i < blocks.Count; i++)
            {
                ModernBlock block = blocks[i];

                if (block.ValueAsStr == value)
                {
                    block.Data = data;
                    blocks[i] = block;
                    break;
                }
            }

            Data[key] = blocks;
        }

        public void SetBlock(string key, uint value, byte[] data)
        {
            List<ModernBlock> blocks = Data[key];

            for (int i = 0; i < blocks.Count; i++)
            {
                ModernBlock block = blocks[i];

                if (block.ValueAsInt == value)
                {
                    block.Data = data;
                    blocks[i] = block;
                    break;
                }
            }

            Data[key] = blocks;
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
            if (!Data.ContainsKey(key))
            {
                return;
            }

            List<ModernBlock> blocks = Data[key];

            foreach (ModernBlock block in blocks)
            {
                if (block.ValueAsStr == value)
                {
                    blocks.Remove(block);
                    break;
                }
            }

            Data[key] = blocks;
        }

        public void DeleteBlock(string key, uint value)
        {
            if (!Data.ContainsKey(key))
            {
                return;
            }

            List<ModernBlock> blocks = Data[key];

            foreach (ModernBlock block in blocks)
            {
                if (block.ValueAsInt == value)
                {
                    blocks.Remove(block);
                    break;
                }
            }

            Data[key] = blocks;
        }

        public PhysicalStoreModern(string tsPath, bool production, PSVersion version)
        {
            TSFile = File.Open(tsPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            Deserialize(PhysStoreCrypto.DecryptPhysicalStore(TSFile.ReadAllBytes(), production, version));
            TSFile.Seek(0, SeekOrigin.Begin);
            Version = version;
            Production = production;
        }

        public void Dispose()
        {
            if (TSFile.CanWrite)
            {
                byte[] data = PhysStoreCrypto.EncryptPhysicalStore(Serialize(), Production, Version);
                TSFile.SetLength(data.LongLength);
                TSFile.Seek(0, SeekOrigin.Begin);
                TSFile.WriteAllBytes(data);
                TSFile.Close();
            }
        }

        public byte[] ReadRaw()
        {
            byte[] data = PhysStoreCrypto.DecryptPhysicalStore(TSFile.ReadAllBytes(), Production, Version);
            TSFile.Seek(0, SeekOrigin.Begin);
            return data;
        }

        public void WriteRaw(byte[] data)
        {
            byte[] encrData = PhysStoreCrypto.EncryptPhysicalStore(data, Production, Version);
            TSFile.SetLength(encrData.LongLength);
            TSFile.Seek(0, SeekOrigin.Begin);
            TSFile.WriteAllBytes(encrData);
            TSFile.Close();
        }

        public IEnumerable<PSBlock> FindBlocks(string valueSearch)
        {
            List<PSBlock> results = new List<PSBlock>();

            foreach (string key in Data.Keys)
            {
                List<ModernBlock> values = Data[key];

                foreach (ModernBlock block in values)
                {
                    if (block.ValueAsStr.Contains(valueSearch))
                    {
                        results.Add(new PSBlock
                        {
                            Type = block.Type,
                            Flags = block.Flags,
                            KeyAsStr = key,
                            Value = block.Value,
                            Data = block.Data
                        });
                    }
                }
            }

            return results;
        }

        public IEnumerable<PSBlock> FindBlocks(uint valueSearch)
        {
            List<PSBlock> results = new List<PSBlock>();

            foreach (string key in Data.Keys)
            {
                List<ModernBlock> values = Data[key];

                foreach (ModernBlock block in values)
                {
                    if (block.ValueAsInt == valueSearch)
                    {
                        results.Add(new PSBlock
                        {
                            Type = block.Type,
                            Flags = block.Flags,
                            KeyAsStr = key,
                            Value = block.Value,
                            Data = block.Data
                        });
                    }
                }
            }

            return results;
        }
    }
}
