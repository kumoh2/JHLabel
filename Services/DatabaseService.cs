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
        public Task<int> SaveLabelAsync(LabelModel label)
        {
            if (label.Id != 0)
                return _connection.UpdateAsync(label);
            else
                return _connection.InsertAsync(label);
        }
    }
}
