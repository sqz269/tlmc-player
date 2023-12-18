namespace SearchService.Model;

public class SearchResult<T>
{
    public string Query { get; set; }
    public T Result { get; set; }
    public long Total { get; set; }
    public long Size { get; set; }
    public long PrevStartIndex { get; set; }
    public long NextStartIndex { get; set; }
}