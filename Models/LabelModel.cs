using Dapper;

namespace JHLabel.Models
{
    public class LabelModel
    {
        public int Id { get; set; }
        public string LabelName { get; set; } = string.Empty;  // 기본값 할당
        public string ZPL { get; set; } = string.Empty;        // 기본값 할당
        public string PGL { get; set; } = string.Empty;        // 기본값 할당
        // 디자인 데이터를 위한 추가 컬럼 (모두 mm 단위 또는 DPI 값)
        public int DPI { get; set; } = 203;            // 예: 203, 300, 600 등
        public double PaperWidthMm { get; set; } = 210;  // 기본 A4 폭
        public double PaperHeightMm { get; set; } = 297; // 기본 A4 높이
    }
}
