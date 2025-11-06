using Microsoft.Data.SqlClient; // Use Microsoft.Data.SqlClient (o mais novo) ou System.Data.SqlClient
using System;

namespace AgendaTatiNails.Repositories
{
    // A classe é 'abstract' (abstrata) porque não queremos usá-la sozinha,
    // apenas queremos que outras classes herdem dela.
    // 'IDisposable' garante que a conexão será fechada corretamente.
    public abstract class DbConnection : IDisposable
    {
        // 'protected' significa que só esta classe e as classes que
        // herdarem dela (como nosso futuro SqlAgendaRepository) podem ver isso.
        protected readonly SqlConnection _connection;

        protected DbConnection(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
        }

        // Este método é exigido pela interface 'IDisposable'
        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this); // Otimização
        }
    }
}