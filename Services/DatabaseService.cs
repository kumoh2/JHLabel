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
                // 이미 존재하면 ID와 ZPL 업데이트 후 저장
                existingLabel.ZPL = label.ZPL;
                return await _connection.UpdateAsync(existingLabel);
            }

            // 없으면 새로 삽입
            return await _connection.InsertAsync(label);
        }
    }
}
