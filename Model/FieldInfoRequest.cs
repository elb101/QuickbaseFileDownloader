namespace QuickbaseFileDownloader.Model
{
    public class FieldInfoRequest
    {
        public string from { get; set; }
        public List<int> select { get; set; }
        public string? where { get; set; }
    }
}
