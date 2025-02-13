namespace LibTSforge.TokenStore
{
    using System;

    public interface ITokenStore : IDisposable
    {
        void Deserialize();
        void Serialize();
        void AddEntry(TokenEntry entry);
        void AddEntries(TokenEntry[] entries);
        void DeleteEntry(string name, string ext);
        void DeleteUnpopEntry(string name, string ext);
        TokenEntry GetEntry(string name, string ext);
        TokenMeta GetMetaEntry(string name);
        void SetEntry(string name, string ext, byte[] data);
    }
}
