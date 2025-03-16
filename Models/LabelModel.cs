using SQLite;

namespace JHLabel.Models
{
    public class LabelModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string LabelName { get; set; }
        public string ZPL { get; set; }
    }
}
