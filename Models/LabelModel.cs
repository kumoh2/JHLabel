using SQLite;

namespace JHLabel.Models
{
    public class LabelModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string LabelName { get; set; } = string.Empty;  // 기본값 할당
        public string ZPL { get; set; } = string.Empty;        // 기본값 할당
    }
}
