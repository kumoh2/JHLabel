using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;
using JHLabel.Models;
using Microsoft.Maui.Controls;
using System.Linq;

namespace JHLabel.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        public DatabaseService(string dbPath)
        {
            _connectionString = $"Data Source={dbPath};";
            InitializeDatabase();
        }

        // 테이블 생성: LabelModel 테이블이 없으면 생성
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS LabelModel (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    LabelName TEXT UNIQUE,
                    ZPL TEXT,
                    PGL TEXT
                );");
        }

        private IDbConnection GetConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public async Task<List<LabelModel>> GetLabelsAsync()
        {
            using var connection = GetConnection();
            var labels = await connection.QueryAsync<LabelModel>("SELECT * FROM LabelModel;");
            return labels.AsList();
        }

        public async Task<int> SaveLabelAsync(LabelModel label)
        {
            using var connection = GetConnection();
            // 동일한 LabelName이 있는지 확인
            var existingLabel = await connection.QueryFirstOrDefaultAsync<LabelModel>(
                "SELECT * FROM LabelModel WHERE LabelName = @LabelName", new { label.LabelName });

            if (existingLabel != null)
            {
                // UI 쪽에서 overwrite 여부를 결정 (기존 코드와 동일하게 mainPage를 사용)
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
                label.Id = existingLabel.Id;
                return await connection.ExecuteAsync(
                    "UPDATE LabelModel SET ZPL = @ZPL, PGL = @PGL WHERE Id = @Id", label);
            }
            else
            {
                return await connection.ExecuteAsync(
                    "INSERT INTO LabelModel (LabelName, ZPL, PGL) VALUES (@LabelName, @ZPL, @PGL)", label);
            }
        }
    }
}
