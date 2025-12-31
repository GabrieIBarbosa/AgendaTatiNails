using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories.Interfaces; // <-- Importa a nova interface
using Microsoft.Data.SqlClient;
using System.Data;

namespace AgendaTatiNails.Repositories
{
    public class SqlServicoRepository : DbConnection, IServicoRepository // <-- Implementa a nova interface
    {
        public SqlServicoRepository(IConfiguration configuration)
            : base(configuration.GetConnectionString("DefaultConnection"))
        {
            // Conexão aberta na classe base
        }

        // (Resumo) Busca um serviço único no banco de dados pelo seu ID.
        public Servico ObterServicoPorId(int id)
        {
            using (var cmd = new SqlCommand("SELECT * FROM Servicos WHERE servicoId = @id", _connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapReaderToServico(reader);
                    }
                }
            }
            return null;
        }

        // (Resumo) Busca uma lista de todos os serviços que estão ativos (status = 1).
        public IEnumerable<Servico> ObterTodosServicos()
        {
            var servicos = new List<Servico>();
            using (var cmd = new SqlCommand("SELECT * FROM Servicos WHERE servicoStatus = 1", _connection)) 
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        servicos.Add(MapReaderToServico(reader));
                    }
                }
            }
            return servicos;
        }

        // (Resumo) Método auxiliar para converter uma linha do banco de dados em um objeto 'Servico'.
        private Servico MapReaderToServico(SqlDataReader reader)
        {
            return new Servico
            {
                ServicoId = (int)reader["servicoId"],
                ServicoDesc = reader["servicoDesc"].ToString(),
                ServicoDuracao = (int)reader["servicoDuracao"],
                ServicoPreco = (decimal)reader["servicoPreco"],
                ServicoStatus = (int)reader["servicoStatus"]
            };
        }
    }
}