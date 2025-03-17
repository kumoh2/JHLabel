using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;
using JHLabel.Models;

namespace JHLabel.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _connection;
        public DatabaseService(string dbPath)
        {
            _connection = new SQLiteAsyncConnection(dbPath);
            _connection.CreateTableAsync<LabelModel>().Wait();
        }
        public Task<List<LabelModel>> GetLabelsAsync() => _connection.Table<LabelModel>().ToListAsync();
        public async Task<int> SaveLabelAsync(LabelModel label)
        {
            // 🔹 동일한 LabelName을 가진 레이블이 있는지 확인
            var existingLabel = await _connection.Table<LabelModel>()
                                                .Where(l => l.LabelName == label.LabelName)
                                                .FirstOrDefaultAsync();

            if (existingLabel != null)
            {
                var mainPage = Application.Current?.Windows.FirstOrDefault()?.Page;

                if (mainPage != null)
                {
                    bool overwrite = await mainPage.DisplayAlert(
                        "Duplicate Label",
                        $"A label named '{label.LabelName}' already exists. Do you want to overwrite it?",
                        "Yes", "No");

                    if (!overwrite)
                    {
                        return 0; // ❌ 사용자가 "No"를 선택하면 아무것도 하지 않음
                    }
                }
                else
                {
                    return 0; // ❌ 예외 처리: mainPage가 null인 경우
                }

                // ✅ "Yes" 선택 시 기존 데이터 업데이트
                existingLabel.ZPL = label.ZPL;
                return await _connection.UpdateAsync(existingLabel);
            }

            // 🔹 중복되지 않는 경우 새로 삽입
            return await _connection.InsertAsync(label);
        }
    }
}
