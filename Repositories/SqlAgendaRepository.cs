using AgendaTatiNails.Models;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;

namespace AgendaTatiNails.Repositories
{
    public class SqlAgendaRepository : DbConnection, IAgendaRepository
    {
        // Precisamos da IConfiguration para ler a string de conexão do appsettings.json
        public SqlAgendaRepository(IConfiguration configuration)
            : base(configuration.GetConnectionString("DefaultConnection"))
        {
            // A classe base (DbConnection) já abre a conexão
        }

        // --- Métodos de Usuário/Cliente ---

        public Usuario ObterUsuarioPorEmail(string email)
        {
            // Usamos 'using' para garantir que o SqlCommand seja descartado
            using (var cmd = new SqlCommand("SELECT * FROM Usuarios WHERE usuarioEmail = @email", _connection))
            {
                cmd.Parameters.AddWithValue("@email", email);

                // Usamos 'using' para garantir que o SqlDataReader seja descartado
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) // Se encontrou um usuário
                    {
                        return MapReaderToUsuario(reader);
                    }
                }
            }
            return null; // Não encontrou
        }

        public Cliente ObterClientePorId(int id)
        {
            // Este SQL junta as tabelas Clientes e Usuarios
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
                        // Mapeia o Cliente
                        var cliente = new Cliente
                        {
                            ClienteId = (int)reader["clienteId"],
                            ClienteTelefone = reader["clienteTelefone"].ToString()
                        };

                        // Mapeia o Usuario associado
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

        public Cliente AdicionarNovoCliente(Cliente novoCliente)
        {
            // Se falhar ao inserir em 'Clientes', precisa reverter a inserção em 'Usuarios'.

            /* 1. Pega a 'connection string' (precisamos criar uma nova conexão
            para garantir que a transação comece limpa).
            (Nota: Esta não é a forma mais eficiente, mas é a mais segura
            para ADO.NET manual, foi o jeito que encontramos de fazer funcionar, pedimos ajuda pra IA nessa).
            *Correção*: reutilizar a conexão '_connection' mas iniciar a transação nela. */

            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // --- ETAPA 1: Inserir na tabela 'Usuarios' ---

                    string sqlUsuario = @"
                        INSERT INTO Usuarios (usuarioEmail, usuarioSenha, usuarioNome)
                        VALUES (@Email, @Senha, @Nome);
                        
                        SELECT SCOPE_IDENTITY();"; // Retorna o ID que acabou de ser gerado

                    int novoUsuarioId;
                    using (var cmdUsuario = new SqlCommand(sqlUsuario, _connection, transaction))
                    {
                        cmdUsuario.Parameters.AddWithValue("@Email", novoCliente.Usuario.UsuarioEmail);
                        cmdUsuario.Parameters.AddWithValue("@Senha", novoCliente.Usuario.UsuarioSenha);
                        cmdUsuario.Parameters.AddWithValue("@Nome", novoCliente.Usuario.UsuarioNome);

                        // ExecuteScalar é usado para obter o primeiro valor retornado (o ID)
                        novoUsuarioId = Convert.ToInt32(cmdUsuario.ExecuteScalar());
                    }

                    // retorna falha
                    if (novoUsuarioId == 0)
                    {
                        throw new Exception("Falha ao obter o ID do novo usuário.");
                    }

                    // --- ETAPA 2: Inserir na tabela 'Clientes' ---

                    string sqlCliente = @"
                        INSERT INTO Clientes (clienteId, clienteTelefone)
                        VALUES (@ClienteId, @Telefone);";

                    using (var cmdCliente = new SqlCommand(sqlCliente, _connection, transaction))
                    {
                        cmdCliente.Parameters.AddWithValue("@ClienteId", novoUsuarioId);
                        cmdCliente.Parameters.AddWithValue("@Telefone", novoCliente.ClienteTelefone);

                        cmdCliente.ExecuteNonQuery();
                    }

                    // --- ETAPA 3: Finzalização ---
                    // Se o código chega aqui então ambas as inserções funcionaram, Só commitar e mandar bala
                    transaction.Commit();


                    novoCliente.ClienteId = novoUsuarioId;
                    novoCliente.Usuario.UsuarioId = novoUsuarioId;
                    return novoCliente;
                }
                catch (Exception ex)
                {
                    // Se vier pra cá, deu algum erro, reverte tudo q foi feito pra n dar conflito.
                    Console.WriteLine($"Erro ao cadastrar novo cliente: {ex.Message}");
                    transaction.Rollback();

                    throw;
                }
            }
        }

        public Atendimento AdicionarAtendimento(int clienteId, int servicoId, int horarioId)
        {
            // Começa a transação
            using (var transaction = _connection.BeginTransaction())
            {
                Atendimento novoAtendimento = new Atendimento();
                Servico servicoAgendado;
                List<Horario> slotsParaMarcar = new List<Horario>();

                try
                {
                    // --- ETAPA 1 : Obter informações do Serviço DENTRO da Transação ---
                    // Não podemos chamar 'ObterServicoPorId(servicoId)' pois ele não conhece a transação.
                    // Trazemos a lógica dele para cá:

                    string sqlServico = "SELECT * FROM Servicos WHERE servicoId = @ServicoId";
                    servicoAgendado = new Servico();

                    using (var cmdServicoInfo = new SqlCommand(sqlServico, _connection, transaction)) // <-- PASSAMOS A TRANSAÇÃO
                    {
                        cmdServicoInfo.Parameters.AddWithValue("@ServicoId", servicoId);
                        using (var reader = cmdServicoInfo.ExecuteReader()) // <-- Este ExecuteReader AGORA FUNCIONA
                        {
                            if (reader.Read())
                            {
                                servicoAgendado.ServicoId = servicoId;
                                servicoAgendado.ServicoDesc = reader["servicoDesc"].ToString();
                                servicoAgendado.ServicoDuracao = (int)reader["servicoDuracao"];
                                servicoAgendado.ServicoPreco = (decimal)reader["servicoPreco"];
                                servicoAgendado.ServicoStatus = (int)reader["servicoStatus"];
                            }
                            else
                            {
                                throw new Exception("Serviço não encontrado.");
                            }
                        } // Reader é fechado aqui. A transação continua.
                    }

                    // Agora o resto do código pode usar o servicoAgendado
                    int slotsNecessarios = (int)Math.Ceiling((double)servicoAgendado.ServicoDuracao / 45.0);


                    // --- ETAPA 2: Encontrar e BLOQUEAR os slots de Horário ---
                    string sqlHorarios = $@"
                        SELECT TOP (@Slots) * FROM Horarios WITH(UPDLOCK) 
                        WHERE horarioData = (SELECT horarioData FROM Horarios WHERE horarioId = @HorarioId)
                          AND horarioPeriodo >= (SELECT horarioPeriodo FROM Horarios WHERE horarioId = @HorarioId)
                        ORDER BY horarioPeriodo";

                    using (var cmdHorarios = new SqlCommand(sqlHorarios, _connection, transaction))
                    {
                        cmdHorarios.Parameters.AddWithValue("@HorarioId", horarioId);
                        cmdHorarios.Parameters.AddWithValue("@Slots", slotsNecessarios);
                        using (var reader = cmdHorarios.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                slotsParaMarcar.Add(new Horario
                                {
                                    HorarioId = (int)reader["horarioId"],
                                    HorarioStatus = (int)reader["horarioStatus"],
                                    HorarioPeriodo = (TimeSpan)reader["horarioPeriodo"], // Pega o TimeSpan
                                    HorarioData = (DateTime)reader["horarioData"] // Pega a Data
                                });
                            }
                        }
                    }

                    // --- ETAPA 3: Validar os Horários ---
                    if (slotsParaMarcar.Count != slotsNecessarios)
                        throw new Exception("Conflito de agendamento: Não há slots consecutivos suficientes.");
                    if (slotsParaMarcar.Any(s => s.HorarioStatus != 1))
                        throw new Exception("Conflito de agendamento: Um dos horários foi ocupado.");

                    // --- ETAPA 4: Criar o Atendimento ---
                    string sqlAtendimento = @"
                        INSERT INTO Atendimentos (
                            atendStatus, atendDataAgend, atendDataAtend, atendPrecoFinal, 
                            idCliente, idColab, pagData, pagStatus
                        ) VALUES (
                            @Status, @DataAgend, @DataAtend, @Preco,
                            @ClienteId, @ColabId, @PagData, @PagStatus
                        );
                        SELECT SCOPE_IDENTITY();";

                    // CORREÇÃO: Pega a data/hora do PRIMEIRO slot (sem 'await')
                    Horario slotPrincipal = slotsParaMarcar.First();
                    DateTime dataHoraAtendimento = slotPrincipal.HorarioData.Date.Add(slotPrincipal.HorarioPeriodo);

                    int novoAtendimentoId;
                    using (var cmdAtendimento = new SqlCommand(sqlAtendimento, _connection, transaction))
                    {
                        cmdAtendimento.Parameters.AddWithValue("@Status", 1); // 1 = Agendado
                        cmdAtendimento.Parameters.AddWithValue("@DataAgend", DateTime.Now);
                        cmdAtendimento.Parameters.AddWithValue("@DataAtend", dataHoraAtendimento);
                        cmdAtendimento.Parameters.AddWithValue("@Preco", servicoAgendado.ServicoPreco);
                        cmdAtendimento.Parameters.AddWithValue("@ClienteId", clienteId);
                        cmdAtendimento.Parameters.AddWithValue("@ColabId", 1); // TODO: Assumindo Colaborador ID=1 (Tati)
                        cmdAtendimento.Parameters.AddWithValue("@PagData", DateTime.Now);
                        cmdAtendimento.Parameters.AddWithValue("@PagStatus", 1); // 1 = Pendente

                        novoAtendimentoId = Convert.ToInt32(cmdAtendimento.ExecuteScalar());
                    }

                    novoAtendimento.AtendId = novoAtendimentoId;

                    // --- ETAPA 5: Ligar o Serviço ao Atendimento ---
                    string sqlServicoAtend = @"
                        INSERT INTO ServicosAtend (idAtend, idServico)
                        VALUES (@AtendId, @ServicoId)";
                    using (var cmdServico = new SqlCommand(sqlServicoAtend, _connection, transaction))
                    {
                        cmdServico.Parameters.AddWithValue("@AtendId", novoAtendimentoId);
                        cmdServico.Parameters.AddWithValue("@ServicoId", servicoId);
                        cmdServico.ExecuteNonQuery();
                    }

                    // --- ETAPA 6: Marcar TODOS os slots como Ocupados ---
                    string idsParaMarcar = string.Join(",", slotsParaMarcar.Select(s => s.HorarioId));
                    string sqlMarcarHorarios = $@"
                        UPDATE Horarios 
                        SET horarioStatus = 2, idAtend = @AtendId
                        WHERE horarioId IN ({idsParaMarcar})";

                    using (var cmdMarcar = new SqlCommand(sqlMarcarHorarios, _connection, transaction))
                    {
                        cmdMarcar.Parameters.AddWithValue("@AtendId", novoAtendimentoId);
                        cmdMarcar.ExecuteNonQuery();
                    }

                    // --- ETAPA 7: Sucesso! ---
                    transaction.Commit();

                    return novoAtendimento;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao adicionar atendimento: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool AtualizarAtendimento(int atendimentoId, int novoServicoId, int novoHorarioId, int clienteId)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // --- ETAPA 1 : Pegar dados do NOVO serviço DENTRO da Transação ---
                    Servico novoServico;
                    string sqlServico = "SELECT * FROM Servicos WHERE servicoId = @ServicoId";

                    using (var cmdServicoInfo = new SqlCommand(sqlServico, _connection, transaction)) // <-- PASSAMOS A TRANSAÇÃO
                    {
                        cmdServicoInfo.Parameters.AddWithValue("@ServicoId", novoServicoId);
                        using (var reader = cmdServicoInfo.ExecuteReader()) 
                        {
                            if (reader.Read())
                            {
                                novoServico = new Servico {
                                    ServicoId = novoServicoId,
                                    ServicoDesc = reader["servicoDesc"].ToString(),
                                    ServicoDuracao = (int)reader["servicoDuracao"],
                                    ServicoPreco = (decimal)reader["servicoPreco"],
                                    ServicoStatus = (int)reader["servicoStatus"]
                                };
                            }
                            else
                            {
                                throw new Exception("Novo serviço não encontrado.");
                            }
                        } // Reader é fechado aqui.
                    }
                    
                    int slotsNecessarios = (int)Math.Ceiling((double)novoServico.ServicoDuracao / 45.0);

                    // --- ETAPA 2: Liberar os SLOTS ANTIGOS ---
                    string sqlLiberarSlots = @"
                        UPDATE Horarios SET horarioStatus = 1, idAtend = NULL
                        WHERE idAtend = @AtendimentoId";
                    using (var cmdLiberar = new SqlCommand(sqlLiberarSlots, _connection, transaction))
                    {
                        cmdLiberar.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdLiberar.ExecuteNonQuery();
                    }

                    // --- ETAPA 3: Encontrar e BLOQUEAR os SLOTS NOVOS ---
                    var slotsParaMarcar = new List<Horario>();
                    string sqlHorarios = $@"
                        SELECT TOP (@Slots) * FROM Horarios WITH(UPDLOCK) 
                        WHERE horarioData = (SELECT horarioData FROM Horarios WHERE horarioId = @HorarioId)
                          AND horarioPeriodo >= (SELECT horarioPeriodo FROM Horarios WHERE horarioId = @HorarioId)
                        ORDER BY horarioPeriodo";

                    using (var cmdHorarios = new SqlCommand(sqlHorarios, _connection, transaction))
                    {
                        cmdHorarios.Parameters.AddWithValue("@HorarioId", novoHorarioId);
                        cmdHorarios.Parameters.AddWithValue("@Slots", slotsNecessarios);
                        using (var reader = cmdHorarios.ExecuteReader())
                        {
                            while (reader.Read()) slotsParaMarcar.Add(new Horario {
                                HorarioId = (int)reader["horarioId"],
                                HorarioStatus = (int)reader["horarioStatus"],
                                HorarioPeriodo = (TimeSpan)reader["horarioPeriodo"],
                                HorarioData = (DateTime)reader["horarioData"]
                            });
                        } 
                    }
                    
                    // --- ETAPA 4: Validar os Horários NOVOS ---
                    if (slotsParaMarcar.Count != slotsNecessarios) throw new Exception("Conflito: Não há slots consecutivos suficientes.");
                    if (slotsParaMarcar.Any(s => s.HorarioStatus != 1)) throw new Exception("Conflito: Um dos horários foi ocupado.");

                    // --- ETAPA 5: Marcar os SLOTS NOVOS ---
                    string idsParaMarcar = string.Join(",", slotsParaMarcar.Select(s => s.HorarioId));
                    string sqlMarcarHorarios = $@"
                        UPDATE Horarios SET horarioStatus = 2, idAtend = @AtendimentoId
                        WHERE horarioId IN ({idsParaMarcar})";
                    using (var cmdMarcar = new SqlCommand(sqlMarcarHorarios, _connection, transaction))
                    {
                        cmdMarcar.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdMarcar.ExecuteNonQuery();
                    }

                    // --- ETAPA 6: Atualizar o Atendimento ---
                    Horario slotPrincipal = slotsParaMarcar.First();
                    DateTime dataHoraAtendimento = slotPrincipal.HorarioData.Date.Add(slotPrincipal.HorarioPeriodo);

                    string sqlAtendimento = @"
                        UPDATE Atendimentos SET
                            atendDataAtend = @DataAtend,
                            atendPrecoFinal = @Preco
                        WHERE atendId = @AtendimentoId AND idCliente = @ClienteId";
                    
                    using (var cmdAtendimento = new SqlCommand(sqlAtendimento, _connection, transaction))
                    {
                        cmdAtendimento.Parameters.AddWithValue("@DataAtend", dataHoraAtendimento);
                        cmdAtendimento.Parameters.AddWithValue("@Preco", novoServico.ServicoPreco);
                        cmdAtendimento.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdAtendimento.Parameters.AddWithValue("@ClienteId", clienteId);
                        
                        int linhas = cmdAtendimento.ExecuteNonQuery();
                        if (linhas == 0) throw new Exception("Atendimento não encontrado ou não pertence ao usuário.");
                    }

                    // --- ETAPA 7: Atualizar ServicosAtend ---
                    string sqlServicos = @"
                        UPDATE ServicosAtend SET idServico = @ServicoId
                        WHERE idAtend = @AtendimentoId";
                    using (var cmdServico = new SqlCommand(sqlServicos, _connection, transaction))
                    {
                        cmdServico.Parameters.AddWithValue("@ServicoId", novoServicoId);
                        cmdServico.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdServico.ExecuteNonQuery();
                    }

                    // --- ETAPA 8: Sucesso! ---
                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao atualizar atendimento: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool CancelarAtendimento(int atendimentoId, int clienteId)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // --- ETAPA 1: Atualiza o status do Atendimento ---
                    string sqlUpdateAtendimento = @"
                        UPDATE Atendimentos
                        SET atendStatus = 3 -- 3 = Cancelado
                        WHERE atendId = @AtendimentoId AND idCliente = @ClienteId";

                    int linhasAfetadas;
                    using (var cmdUpdate = new SqlCommand(sqlUpdateAtendimento, _connection, transaction))
                    {
                        cmdUpdate.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdUpdate.Parameters.AddWithValue("@ClienteId", clienteId);
                        linhasAfetadas = cmdUpdate.ExecuteNonQuery();
                    }

                    // Se 0 linhas foram afetadas então o agendamento n existe ou n pertence ao cliente.
                    if (linhasAfetadas == 0)
                    {
                        throw new Exception("Agendamento não encontrado ou não pertence ao usuário.");
                    }

                    // --- ETAPA 2: Libera os Horários associados ---
                    // reseta todos os slots ligados a este atendimento.
                    string sqlUpdateHorarios = @"
                        UPDATE Horarios
                        SET horarioStatus = 1, -- 1 = Disponível
                            idAtend = NULL
                        WHERE idAtend = @AtendimentoId";

                    using (var cmdHorarios = new SqlCommand(sqlUpdateHorarios, _connection, transaction))
                    {
                        cmdHorarios.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdHorarios.ExecuteNonQuery();
                    }

                    // --- ETAPA 3: Finalizado ---
                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao cancelar atendimento: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public bool MarcarAtendimentoConcluido(int atendimentoId)
        {
            try
            {
                string sql = @"
                    UPDATE Atendimentos
                    SET atendStatus = 2 -- 2 = Concluído
                    WHERE atendId = @AtendimentoId";

                using (var cmd = new SqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                    int linhasAfetadas = cmd.ExecuteNonQuery();
                    return linhasAfetadas > 0; // Retorna true se atualizou 1 linha
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao marcar atendimento como concluído: {ex.Message}");
                throw;
            }
        }

        public bool CancelarAtendimentoAdmin(int atendimentoId)
        {
            // Esta lógica é IDÊNTICA à do cliente, mas não checa o clienteId
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // --- ETAPA 1: Atualiza o status do Atendimento ---
                    string sqlUpdateAtendimento = @"
                        UPDATE Atendimentos
                        SET atendStatus = 3 -- 3 = Cancelado
                        WHERE atendId = @AtendimentoId";

                    int linhasAfetadas;
                    using (var cmdUpdate = new SqlCommand(sqlUpdateAtendimento, _connection, transaction))
                    {
                        cmdUpdate.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        linhasAfetadas = cmdUpdate.ExecuteNonQuery();
                    }

                    if (linhasAfetadas == 0)
                    {
                        throw new Exception("Agendamento não encontrado.");
                    }

                    // --- ETAPA 2: Libera os Horários associados ---
                    string sqlUpdateHorarios = @"
                        UPDATE Horarios
                        SET horarioStatus = 1, -- 1 = Disponível
                            idAtend = NULL
                        WHERE idAtend = @AtendimentoId";

                    using (var cmdHorarios = new SqlCommand(sqlUpdateHorarios, _connection, transaction))
                    {
                        cmdHorarios.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdHorarios.ExecuteNonQuery();
                    }

                    // --- ETAPA 3: Finalizado ---
                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao cancelar atendimento (Admin): {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // --- Métodos de Serviço ---

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

        public IEnumerable<Servico> ObterTodosServicos()
        {
            var servicos = new List<Servico>();
            using (var cmd = new SqlCommand("SELECT * FROM Servicos WHERE servicoStatus = 1", _connection)) // Ex: 1 = Ativo
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


        // --- Métodos de Atendimento ---

        public Atendimento ObterAtendimentoPorId(int id)
        {
            // Esta query é complexa: busca o Atendimento, o Cliente, o Colaborador
            // e também os Serviços associados

            Atendimento atendimento = null;

            // 1. Obter o Atendimento principal e dados do Cliente/Colaborador
            string sqlAtendimento = @"
                SELECT a.*, 
                       c.clienteTelefone, uc.usuarioNome as ClienteNome, uc.usuarioEmail as ClienteEmail,
                       ucol.usuarioNome as ColaboradorNome
                FROM Atendimentos a
                JOIN Clientes c ON a.idCliente = c.clienteId
                JOIN Usuarios uc ON c.clienteId = uc.usuarioId
                JOIN Colaboradores col ON a.idColab = col.colabId
                JOIN Usuarios ucol ON col.colabId = ucol.usuarioId
                WHERE a.atendId = @id";

            using (var cmd = new SqlCommand(sqlAtendimento, _connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        atendimento = MapReaderToAtendimento(reader);
                    }
                } // O reader é fechado aqui
            }

            // Se não encontrou o atendimento, não faz sentido buscar os serviços
            if (atendimento == null) return null;

            // 2. Obter os Serviços associados a este atendimento
            string sqlServicos = @"
                SELECT s.* FROM Servicos s
                JOIN ServicosAtend sa ON s.servicoId = sa.idServico
                WHERE sa.idAtend = @id";

            using (var cmd = new SqlCommand(sqlServicos, _connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // Adiciona cada serviço na lista de serviços do atendimento
                        atendimento.Servicos.Add(MapReaderToServico(reader));
                    }
                }
            }

            return atendimento;
        }

        public IEnumerable<Atendimento> ObterAtendimentosPorCliente(int clienteId)
        {
            // Dicionário para evitar duplicatas (N+1)
            var dictionaryAtendimentos = new Dictionary<int, Atendimento>();
            
            // Esta query agora é uma cópia da 'ObterTodosAtendimentos',
            // mas com o 'WHERE a.idCliente = @clienteId'
            string sql = @"
                SELECT 
                    a.atendId, a.atendStatus, a.atendDataAgend, a.atendDataAtend, a.atendPrecoFinal, a.idColab,
                    
                    ucol.usuarioNome as ColaboradorNome,
                    
                    s.servicoId, s.servicoDesc, s.servicoPreco, s.servicoDuracao
                FROM Atendimentos a
                
                INNER JOIN Colaboradores col ON a.idColab = col.colabId
                INNER JOIN Usuarios ucol ON col.colabId = ucol.usuarioId
                
                -- LEFT JOIN para buscar os serviços
                LEFT JOIN ServicosAtend sa ON a.atendId = sa.idAtend
                LEFT JOIN Servicos s ON sa.idServico = s.servicoId
                
                WHERE a.idCliente = @clienteId
                ORDER BY a.atendDataAtend DESC";
            
            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@clienteId", clienteId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int atendimentoId = (int)reader["atendId"];
                        Atendimento atendimento;

                        // Se é a primeira vez que vemos este Atendimento
                        if (!dictionaryAtendimentos.TryGetValue(atendimentoId, out atendimento))
                        {
                            atendimento = new Atendimento
                            {
                                AtendId = atendimentoId,
                                AtendStatus = (int)reader["atendStatus"],
                                AtendDataAgend = (DateTime)reader["atendDataAgend"],
                                AtendDataAtend = (DateTime)reader["atendDataAtend"],
                                AtendPrecoFinal = reader["atendPrecoFinal"] as decimal?,
                                IdColab = (int)reader["idColab"],
                                IdCliente = clienteId, // Nós já sabemos o ID do cliente
                                Colaborador = new Colaborador
                                {
                                    ColabId = (int)reader["idColab"],
                                    Usuario = new Usuario { UsuarioNome = reader["ColaboradorNome"].ToString() }
                                }
                            };
                            dictionaryAtendimentos.Add(atendimentoId, atendimento);
                        }

                        // Adiciona o Serviço a este atendimento (se ele existir)
                        if (reader["servicoId"] != DBNull.Value)
                        {
                            atendimento.Servicos.Add(new Servico
                            {
                                ServicoId = (int)reader["servicoId"],
                                ServicoDesc = reader["servicoDesc"].ToString(),
                                ServicoPreco = (decimal)reader["servicoPreco"],
                                ServicoDuracao = (int)reader["servicoDuracao"]
                            });
                        }
                    }
                }
            }
            return dictionaryAtendimentos.Values;
        }

        public IEnumerable<Atendimento> ObterTodosAtendimentos()
        {
            var atendimentos = new List<Atendimento>();
            var dictionaryAtendimentos = new Dictionary<int, Atendimento>();

            // Esta é uma query complexa que busca TUDO de uma vez
            // 1. Atendimentos
            // 2. Dados do Cliente (via JOIN com Usuarios)
            // 3. Dados do Colaborador (via JOIN com Usuarios)
            // 4. Serviços de CADA atendimento (via LEFT JOIN com ServicosAtend e Servicos)
            // O LEFT JOIN é importante para trazer atendimentos que (por erro) não tenham serviços
            string sql = @"
                SELECT 
                    a.atendId, a.atendStatus, a.atendDataAgend, a.atendDataAtend, a.atendPrecoFinal, a.idColab,
                    
                    c.clienteId, uc.usuarioNome as ClienteNome,
                    
                    ucol.usuarioNome as ColaboradorNome,
                    
                    s.servicoId, s.servicoDesc, s.servicoPreco, s.servicoDuracao
                FROM Atendimentos a
                
                INNER JOIN Clientes c ON a.idCliente = c.clienteId
                INNER JOIN Usuarios uc ON c.clienteId = uc.usuarioId
                
                INNER JOIN Colaboradores col ON a.idColab = col.colabId
                INNER JOIN Usuarios ucol ON col.colabId = ucol.usuarioId
                
                LEFT JOIN ServicosAtend sa ON a.atendId = sa.idAtend
                LEFT JOIN Servicos s ON sa.idServico = s.servicoId
                
                ORDER BY a.atendDataAtend DESC";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int atendimentoId = (int)reader["atendId"];
                        Atendimento atendimento;

                        // Se este é o primeiro serviço que vemos para este atendimento...
                        if (!dictionaryAtendimentos.TryGetValue(atendimentoId, out atendimento))
                        {
                            // ...cria o objeto Atendimento
                            atendimento = new Atendimento
                            {
                                AtendId = atendimentoId,
                                AtendStatus = (int)reader["atendStatus"],
                                AtendDataAgend = (DateTime)reader["atendDataAgend"],
                                AtendDataAtend = (DateTime)reader["atendDataAtend"],
                                AtendPrecoFinal = reader["atendPrecoFinal"] as decimal?,
                                IdColab = (int)reader["idColab"],
                                IdCliente = (int)reader["clienteId"],

                                // Cria o Cliente (parcial)
                                Cliente = new Cliente
                                {
                                    ClienteId = (int)reader["clienteId"],
                                    Usuario = new Usuario { UsuarioNome = reader["ClienteNome"].ToString() }
                                },
                                // Cria o Colaborador (parcial)
                                Colaborador = new Colaborador
                                {
                                    ColabId = (int)reader["idColab"],
                                    Usuario = new Usuario { UsuarioNome = reader["ColaboradorNome"].ToString() }
                                }
                            };
                            dictionaryAtendimentos.Add(atendimentoId, atendimento);
                        }

                        // Adiciona o Serviço a este atendimento
                        // (Se servicoId não for nulo)
                        if (reader["servicoId"] != DBNull.Value)
                        {
                            atendimento.Servicos.Add(new Servico
                            {
                                ServicoId = (int)reader["servicoId"],
                                ServicoDesc = reader["servicoDesc"].ToString(),
                                ServicoPreco = (decimal)reader["servicoPreco"],
                                ServicoDuracao = (int)reader["servicoDuracao"]
                            });
                        }
                    }
                }
            }
            return dictionaryAtendimentos.Values;
        }


        // --- MÉTODOS DE LÓGICA DE AGENDAMENTO ---
        public Horario ObterHorarioPorAtendimentoId(int atendimentoId)
        {
            // Busca o PRIMEIRO slot de um atendimento (para casos de 90min)
            string sql = @"
                SELECT TOP 1 * FROM Horarios
                WHERE idAtend = @AtendimentoId
                ORDER BY horarioPeriodo ASC";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Horario
                        {
                            HorarioId = (int)reader["horarioId"],
                            HorarioStatus = (int)reader["horarioStatus"],
                            HorarioPeriodo = (TimeSpan)reader["horarioPeriodo"],
                            HorarioData = (DateTime)reader["horarioData"],
                            IdAtend = reader["idAtend"] as int?
                        };
                    }
                }
            }
            return null; // Não encontrou
        }
        public IEnumerable<Horario> ObterHorariosDisponiveis(DateTime data, int duracaoTotal)
        {
            // --- ETAPA 1: Garantir que os slots para o dia existem ---

            try
            {
                using (var cmd = new SqlCommand("sp_GarantirHorariosParaData", _connection))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Data", data.Date);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico ao garantir horários: {ex.Message}");
                return new List<Horario>();
            }

            // --- ETAPA 2: Lógica de Busca de Slots 

            int slotsNecessarios = (int)Math.Ceiling((double)duracaoTotal / 45.0);

            var horarios = new List<Horario>();
            string sql;

            if (slotsNecessarios <= 1)
            {
                // A query simples para 45min (está correta)
                sql = @"
                    SELECT * FROM Horarios 
                    WHERE horarioData = @data AND horarioStatus = 1 
                      AND DATEADD(day, DATEDIFF(day, 0, horarioData), CAST(horarioPeriodo AS DATETIME)) > GETDATE()
                    ORDER BY horarioPeriodo";
            }
            else
            {
                // A query complexa para 90min+ 
                sql = $@"
                    WITH SlotsComJanela AS (
                        SELECT 
                            *,
                            -- 1. Conta quantos slots OCUPADOS (status != 1) existem na janela
                            SUM(CASE WHEN horarioStatus != 1 THEN 1 ELSE 0 END) 
                                OVER (
                                    ORDER BY horarioPeriodo 
                                    ROWS BETWEEN CURRENT ROW AND {slotsNecessarios - 1} FOLLOWING
                                ) as SlotsOcupadosNaJanela,
                            
                            -- 2. Pega o horário DO ÚLTIMO slot na janela
                            LEAD(horarioPeriodo, {slotsNecessarios - 1}) 
                                OVER (
                                    ORDER BY horarioPeriodo
                                ) as PeriodoFinalDaJanela
                        FROM 
                            Horarios
                        WHERE 
                            horarioData = @data
                    )
                    SELECT * FROM SlotsComJanela
                    WHERE 
                        SlotsOcupadosNaJanela = 0 -- 1. Se a contagem de ocupados é 0...
                        AND PeriodoFinalDaJanela IS NOT NULL -- 2. ...e o último slot existe (não 'caiu' do fim do dia)
                        
                        -- 3. ****** A CORREÇÃO IMPORTANTE ******
                        -- Garante que os slots são temporalmente consecutivos (sem pular o almoço)
                        -- (Se 2 slots, a diferença deve ser 45 min. Se 3 slots, 90 min. etc.)
                        AND DATEDIFF(minute, horarioPeriodo, PeriodoFinalDaJanela) = {(slotsNecessarios - 1) * 45} 

                        AND DATEADD(day, DATEDIFF(day, 0, horarioData), CAST(horarioPeriodo AS DATETIME)) > GETDATE() -- 4. ...e é no futuro
                    ORDER BY 
                        horarioPeriodo;
                ";
            }


            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@data", data.Date);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        horarios.Add(new Horario
                        {
                            HorarioId = (int)reader["horarioId"],
                            HorarioStatus = (int)reader["horarioStatus"],
                            HorarioPeriodo = (TimeSpan)reader["horarioPeriodo"],
                            HorarioData = (DateTime)reader["horarioData"],
                            IdAtend = reader["idAtend"] as int?
                        });
                    }
                }
            }
            return horarios;
        }

        // --- MÉTODOS "HELPER" PARA MAPEAMENTO ---
        // Estes métodos ajudam a não repetir código

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

        private Atendimento MapReaderToAtendimento(SqlDataReader reader)
        {
            // Este método mapeia a query complexa do ObterAtendimentoPorId
            var atendimento = new Atendimento
            {
                AtendId = (int)reader["atendId"],
                AtendStatus = (int)reader["atendStatus"],
                AtendDataAgend = (DateTime)reader["atendDataAgend"],
                AtendDataAtend = (DateTime)reader["atendDataAtend"],
                AtendObs = reader["atendObs"].ToString(),
                AtendPrecoFinal = reader["atendPrecoFinal"] as decimal?,
                IdCliente = (int)reader["idCliente"],
                IdColab = (int)reader["idColab"],
                PagData = (DateTime)reader["pagData"],
                PagStatus = (int)reader["pagStatus"],

                // Preenche o Cliente com seus dados vindos do JOIN
                Cliente = new Cliente
                {
                    ClienteId = (int)reader["idCliente"],
                    ClienteTelefone = reader["clienteTelefone"].ToString(),
                    Usuario = new Usuario
                    {
                        UsuarioId = (int)reader["idCliente"],
                        UsuarioNome = reader["ClienteNome"].ToString(),
                        UsuarioEmail = reader["ClienteEmail"].ToString()
                    }
                },

                // Preenche o Colaborador com seus dados vindos do JOIN
                Colaborador = new Colaborador
                {
                    ColabId = (int)reader["idColab"],
                    Usuario = new Usuario
                    {
                        UsuarioId = (int)reader["idColab"],
                        UsuarioNome = reader["ColaboradorNome"].ToString()
                    }
                }
            };

            return atendimento;
        }
    }
}
