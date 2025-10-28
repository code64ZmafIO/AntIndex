namespace AntIndex.Services.Splitting;

public interface IPhraseSplitter
{
    string[] Tokenize(string? value);
}
