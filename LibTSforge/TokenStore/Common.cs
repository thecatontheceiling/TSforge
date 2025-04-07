namespace LibTSforge.TokenStore
{
    using System.Collections.Generic;
    using System.IO;

    public class TokenEntry
    {
        public string Name;
        public string Extension;
        public byte[] Data;
        public bool Populated;
    }

    public class TokenMeta
    {
        public string Name;
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();

        public byte[] Serialize()
        {
            BinaryWriter writer = new BinaryWriter(new MemoryStream());
            writer.Write(1);
            byte[] nameBytes = Utils.EncodeString(Name);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);

            foreach (KeyValuePair<string, string> kv in Data)
            {
                byte[] keyBytes = Utils.EncodeString(kv.Key);
                byte[] valueBytes = Utils.EncodeString(kv.Value);
                writer.Write(keyBytes.Length);
                writer.Write(valueBytes.Length);
                writer.Write(keyBytes);
                writer.Write(valueBytes);
            }

            return writer.GetBytes();
        }

        private void Deserialize(byte[] data)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(data));
            reader.ReadInt32();
            int nameLen = reader.ReadInt32();
            Name = reader.ReadNullTerminatedString(nameLen);

            while (reader.BaseStream.Position < data.Length - 0x8)
            {
                int keyLen = reader.ReadInt32();
                int valueLen = reader.ReadInt32();
                string key = reader.ReadNullTerminatedString(keyLen);
                string value = reader.ReadNullTerminatedString(valueLen);
                Data[key] = value;
            }
        }

        public TokenMeta(byte[] data)
        {
            Deserialize(data);
        }

        public TokenMeta()
        {

        }
    }
}
