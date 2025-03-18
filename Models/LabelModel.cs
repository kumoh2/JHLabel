using Dapper;

namespace JHLabel.Models
{
    public class LabelModel
    {
        public int Id { get; set; }
        public string LabelName { get; set; } = string.Empty;  // 기본값 할당
        public string ZPL { get; set; } = string.Empty;        // 기본값 할당
        public string PGL { get; set; } = string.Empty;        // 기본값 할당
    }
}
