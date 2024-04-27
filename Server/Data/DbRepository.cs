using System;
using System.Data;
using Dapper;
using MySql.Data.MySqlClient;


namespace TriangleDbRepository
{
    public class DbRepository
    {
        //private IDbConnection _dbConnection;

        //public DbRepository(string connectionString)
        //{
        //    _dbConnection = new SqliteConnection(connectionString);
        //}

        private IDbConnection _dbConnection;

        public DbRepository(IConfiguration config)
        {
            _dbConnection = new MySqlConnection(config.GetConnectionString("DefaultConnection"));
        }


        public void OpenConnection()
        {
            //if there is no connection to the DB
            if (_dbConnection.State != ConnectionState.Open)
            {
                //Create new connection to the db
                _dbConnection.Open();
            }
        }

        public void CloseConnection()
        {
            _dbConnection.Close();
        }

        //parameters = query parameters
        public async Task<IEnumerable<T>> GetRecordsAsync<T>(string query, object parameters)
        {
            try
            {
                OpenConnection();

                //Query - Method from dapper
                //Query<RETURN_TYPE>();
                IEnumerable<T> records = await _dbConnection.QueryAsync<T>(query, parameters, commandType: CommandType.Text);

                CloseConnection();
                return records;
            }
            catch (Exception ex)
            {
                CloseConnection();
                throw;
            }
        }

        public async Task<bool> SaveDataAsync(string query, object parameters)
        {
            try
            {
                OpenConnection();

                //Return the amount of saved records 
                int records = await _dbConnection.ExecuteAsync(query, parameters, commandType: CommandType.Text);

                CloseConnection();

                //if one records or more updated - return true. else return false
                return records > 0;
            }
            catch (Exception ex)
            {
                CloseConnection();
                throw;
            }
        }

        public async Task<int> InsertReturnId(string query, object parameters)
        {
            try
            {
                OpenConnection();
                int results = await _dbConnection.ExecuteAsync(sql: query, param: parameters, commandType: CommandType.Text);

                if (results > 0)
                {
                    int Id = _dbConnection.Query<int>("SELECT LAST_INSERT_ID()").FirstOrDefault();
                    CloseConnection();
                    return Id;
                }
                CloseConnection();
                return 0;
            }
            catch (System.Exception)
            {
                CloseConnection();
                throw;
            }
        }

    }
}