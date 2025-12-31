using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories.Interfaces; // <-- Importa a nova interface
using Microsoft.Data.SqlClient;
using System.Data;

namespace AgendaTatiNails.Repositories
{
    public class SqlUsuarioRepository : DbConnection, IUsuarioRepository // <-- Implementa a nova interface
    {
        public SqlUsuarioRepository(IConfiguration configuration)
            : base(configuration.GetConnectionString("DefaultConnection"))
        {
            // Conexão aberta na classe base
        }

        // (Resumo) Busca um usuário no banco de dados pelo seu e-mail (usado para o login).
        public Usuario ObterUsuarioPorEmail(string email)
        {
            using (var cmd = new SqlCommand("SELECT * FROM Usuarios WHERE usuarioEmail = @email", _connection))
            {
                cmd.Parameters.AddWithValue("@email", email);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapReaderToUsuario(reader);
                    }
                }
            }
            return null;
        }

        // (Resumo) Busca um cliente e seus dados de usuário (nome, email) pelo ID.
        public Cliente ObterClientePorId(int id)
        {
            string sql = @"
                SELECT c.*, u.usuarioNome, u.usuarioEmail, u.usuarioSenha
                FROM Clientes c
                JOIN Usuarios u ON c.clienteId = u.usuarioId
                WHERE c.clienteId = @id";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var cliente = new Cliente
                        {
                            ClienteId = (int)reader["clienteId"],
                            ClienteTelefone = reader["clienteTelefone"].ToString()
                        };
                        cliente.Usuario = new Usuario
                        {
                            UsuarioId = (int)reader["clienteId"],
                            UsuarioNome = reader["usuarioNome"].ToString(),
                            UsuarioEmail = reader["usuarioEmail"].ToString(),
                            UsuarioSenha = reader["usuarioSenha"].ToString()
                        };
                        return cliente;
                    }
                }
            }
            return null;
        }

        // (Resumo) Cadastra um novo cliente. Usa uma transação para salvar em 'Usuarios' e 'Clientes'.
        public Cliente AdicionarNovoCliente(Cliente novoCliente)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    string sqlUsuario = @"
                        INSERT INTO Usuarios (usuarioEmail, usuarioSenha, usuarioNome)
                        VALUES (@Email, @Senha, @Nome);
                        SELECT SCOPE_IDENTITY();"; 

                    int novoUsuarioId;
                    using (var cmdUsuario = new SqlCommand(sqlUsuario, _connection, transaction))
                    {
                        cmdUsuario.Parameters.AddWithValue("@Email", novoCliente.Usuario.UsuarioEmail);
                        cmdUsuario.Parameters.AddWithValue("@Senha", novoCliente.Usuario.UsuarioSenha);
                        cmdUsuario.Parameters.AddWithValue("@Nome", novoCliente.Usuario.UsuarioNome);
                        novoUsuarioId = Convert.ToInt32(cmdUsuario.ExecuteScalar());
                    }

                    if (novoUsuarioId == 0)
                    {
                        throw new Exception("Falha ao obter o ID do novo usuário.");
                    }

                    string sqlCliente = @"
                        INSERT INTO Clientes (clienteId, clienteTelefone)
                        VALUES (@ClienteId, @Telefone);";

                    using (var cmdCliente = new SqlCommand(sqlCliente, _connection, transaction))
                    {
                        cmdCliente.Parameters.AddWithValue("@ClienteId", novoUsuarioId);
                        cmdCliente.Parameters.AddWithValue("@Telefone", novoCliente.ClienteTelefone);
                        cmdCliente.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    novoCliente.ClienteId = novoUsuarioId;
                    novoCliente.Usuario.UsuarioId = novoUsuarioId;
                    return novoCliente;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao cadastrar novo cliente: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // (Resumo) Método auxiliar para converter uma linha do banco de dados em um objeto 'Usuario'.
        private Usuario MapReaderToUsuario(SqlDataReader reader)
        {
            return new Usuario
            {
                UsuarioId = (int)reader["usuarioId"],
                UsuarioEmail = reader["usuarioEmail"].ToString(),
                UsuarioSenha = reader["usuarioSenha"].ToString(),
                UsuarioNome = reader["usuarioNome"].ToString()
            };
        }
    }
}