using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;
using AgendaTatiNails.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace AgendaTatiNails.Repositories
{
    public class SqlAtendimentoRepository : DbConnection, IAtendimentoRepository
    {
        public SqlAtendimentoRepository(IConfiguration configuration)
            : base(configuration.GetConnectionString("DefaultConnection"))
        {
        }

        // Cria um novo agendamento. 
        // ALTERAÇÃO: Agora recebe 'string obs' opcional e salva no banco.
        public Atendimento AdicionarAtendimento(int clienteId, int servicoId, int horarioId, string obs = null)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                Atendimento novoAtendimento = new Atendimento();
                Servico servicoAgendado;
                List<Horario> slotsParaMarcar = new List<Horario>();

                try
                {
                    // --- ETAPA 1: Obter informações do Serviço ---
                    string sqlServico = "SELECT * FROM Servicos WHERE servicoId = @ServicoId";
                    servicoAgendado = new Servico();
                    using (var cmdServicoInfo = new SqlCommand(sqlServico, _connection, transaction))
                    {
                        cmdServicoInfo.Parameters.AddWithValue("@ServicoId", servicoId);
                        using (var reader = cmdServicoInfo.ExecuteReader())
                        {
                            if (reader.Read()) servicoAgendado = MapReaderToServico(reader);
                            else throw new Exception("Serviço não encontrado.");
                        }
                    }
                    int slotsNecessarios = (int)Math.Ceiling((double)servicoAgendado.ServicoDuracao / 45.0);

                    // --- ETAPA 2: Encontrar e BLOQUEAR os slots ---
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
                                slotsParaMarcar.Add(new Horario {
                                    HorarioId = (int)reader["horarioId"], HorarioStatus = (int)reader["horarioStatus"],
                                    HorarioPeriodo = (TimeSpan)reader["horarioPeriodo"], HorarioData = (DateTime)reader["horarioData"]
                                });
                            }
                        }
                    }

                    // --- ETAPA 3: Validar ---
                    if (slotsParaMarcar.Count != slotsNecessarios) throw new Exception("Conflito: Slots insuficientes.");
                    if (slotsParaMarcar.Any(s => s.HorarioStatus != 1)) throw new Exception("Conflito: Horário ocupado.");

                    // --- ETAPA 4: Criar o Atendimento (COM OBSERVAÇÃO) ---
                    string sqlAtendimento = @"
                        INSERT INTO Atendimentos 
                        (atendStatus, atendDataAgend, atendDataAtend, atendPrecoFinal, idCliente, idColab, idServico, pagData, pagStatus, atendObs) 
                        VALUES 
                        (@Status, @DataAgend, @DataAtend, @Preco, @ClienteId, @ColabId, @ServicoId, @PagData, @PagStatus, @Obs);
                        SELECT SCOPE_IDENTITY();";
                    
                    Horario slotPrincipal = slotsParaMarcar.First();
                    DateTime dataHoraAtendimento = slotPrincipal.HorarioData.Date.Add(slotPrincipal.HorarioPeriodo);

                    int novoAtendimentoId;
                    using (var cmdAtendimento = new SqlCommand(sqlAtendimento, _connection, transaction))
                    {
                        cmdAtendimento.Parameters.AddWithValue("@Status", 1); 
                        cmdAtendimento.Parameters.AddWithValue("@DataAgend", DateTime.Now);
                        cmdAtendimento.Parameters.AddWithValue("@DataAtend", dataHoraAtendimento);
                        cmdAtendimento.Parameters.AddWithValue("@Preco", servicoAgendado.ServicoPreco);
                        cmdAtendimento.Parameters.AddWithValue("@ClienteId", clienteId);
                        cmdAtendimento.Parameters.AddWithValue("@ColabId", 1);
                        cmdAtendimento.Parameters.AddWithValue("@ServicoId", servicoId); 
                        cmdAtendimento.Parameters.AddWithValue("@PagData", DateTime.Now);
                        cmdAtendimento.Parameters.AddWithValue("@PagStatus", 1);
                        
                        // Se obs for nulo, grava NULL no banco
                        cmdAtendimento.Parameters.AddWithValue("@Obs", (object)obs ?? DBNull.Value); 

                        novoAtendimentoId = Convert.ToInt32(cmdAtendimento.ExecuteScalar());
                    }
                    novoAtendimento.AtendId = novoAtendimentoId;

                    // --- ETAPA 5: Marcar Horários ---
                    string idsParaMarcar = string.Join(",", slotsParaMarcar.Select(s => s.HorarioId));
                    string sqlMarcarHorarios = $@"UPDATE Horarios SET horarioStatus = 2, idAtend = @AtendId WHERE horarioId IN ({idsParaMarcar})";
                    using (var cmdMarcar = new SqlCommand(sqlMarcarHorarios, _connection, transaction))
                    {
                        cmdMarcar.Parameters.AddWithValue("@AtendId", novoAtendimentoId);
                        cmdMarcar.ExecuteNonQuery();
                    }

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

        // Edita um agendamento.
        // ALTERAÇÃO: Agora recebe 'string obs' opcional e atualiza no banco.
        public bool AtualizarAtendimento(int atendimentoId, int novoServicoId, int novoHorarioId, int clienteId, string obs = null)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // 1. Dados do Novo Serviço
                    Servico novoServico;
                    string sqlServico = "SELECT * FROM Servicos WHERE servicoId = @ServicoId";
                    using (var cmdServicoInfo = new SqlCommand(sqlServico, _connection, transaction)) 
                    {
                        cmdServicoInfo.Parameters.AddWithValue("@ServicoId", novoServicoId);
                        using (var reader = cmdServicoInfo.ExecuteReader())
                        {
                            if (reader.Read()) novoServico = MapReaderToServico(reader);
                            else throw new Exception("Novo serviço não encontrado.");
                        }
                    }
                    int slotsNecessarios = (int)Math.Ceiling((double)novoServico.ServicoDuracao / 45.0);

                    // 2. Liberar Slots Antigos
                    string sqlLiberarSlots = @"UPDATE Horarios SET horarioStatus = 1, idAtend = NULL WHERE idAtend = @AtendimentoId";
                    using (var cmdLiberar = new SqlCommand(sqlLiberarSlots, _connection, transaction))
                    {
                        cmdLiberar.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdLiberar.ExecuteNonQuery();
                    }

                    // 3. Bloquear Novos Slots
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
                                HorarioId = (int)reader["horarioId"], HorarioStatus = (int)reader["horarioStatus"],
                                HorarioPeriodo = (TimeSpan)reader["horarioPeriodo"], HorarioData = (DateTime)reader["horarioData"]
                            });
                        }
                    }
                    
                    if (slotsParaMarcar.Count != slotsNecessarios) throw new Exception("Conflito: Slots insuficientes.");
                    if (slotsParaMarcar.Any(s => s.HorarioStatus != 1)) throw new Exception("Conflito: Horário ocupado.");

                    // 4. Marcar Novos Slots
                    string idsParaMarcar = string.Join(",", slotsParaMarcar.Select(s => s.HorarioId));
                    string sqlMarcarHorarios = $@"UPDATE Horarios SET horarioStatus = 2, idAtend = @AtendimentoId WHERE horarioId IN ({idsParaMarcar})";
                    using (var cmdMarcar = new SqlCommand(sqlMarcarHorarios, _connection, transaction))
                    {
                        cmdMarcar.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdMarcar.ExecuteNonQuery();
                    }

                    // 5. Atualizar o Atendimento (Incluindo OBS)
                    Horario slotPrincipal = slotsParaMarcar.First();
                    DateTime dataHoraAtendimento = slotPrincipal.HorarioData.Date.Add(slotPrincipal.HorarioPeriodo);
                    
                    string sqlAtendimento = @"
                        UPDATE Atendimentos SET 
                            atendDataAtend = @DataAtend, 
                            atendPrecoFinal = @Preco,
                            idServico = @ServicoId,
                            atendObs = @Obs
                        WHERE atendId = @AtendimentoId AND idCliente = @ClienteId";
                    
                    using (var cmdAtendimento = new SqlCommand(sqlAtendimento, _connection, transaction))
                    {
                        cmdAtendimento.Parameters.AddWithValue("@DataAtend", dataHoraAtendimento);
                        cmdAtendimento.Parameters.AddWithValue("@Preco", novoServico.ServicoPreco);
                        cmdAtendimento.Parameters.AddWithValue("@ServicoId", novoServicoId);
                        cmdAtendimento.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdAtendimento.Parameters.AddWithValue("@ClienteId", clienteId);
                        cmdAtendimento.Parameters.AddWithValue("@Obs", (object)obs ?? DBNull.Value);

                        int linhas = cmdAtendimento.ExecuteNonQuery();
                        if (linhas == 0) throw new Exception("Atendimento não encontrado.");
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao atualizar: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // Cancela um agendamento e salva o motivo (OBRIGATÓRIO).
        public bool CancelarAtendimento(int atendimentoId, int clienteId, string motivo)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    string sqlUpdateAtendimento = @"
                        UPDATE Atendimentos 
                        SET atendStatus = 3, 
                            atendObs = @Motivo 
                        WHERE atendId = @AtendimentoId AND idCliente = @ClienteId";

                    using (var cmdUpdate = new SqlCommand(sqlUpdateAtendimento, _connection, transaction))
                    {
                        cmdUpdate.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdUpdate.Parameters.AddWithValue("@ClienteId", clienteId);
                        cmdUpdate.Parameters.AddWithValue("@Motivo", (object)motivo ?? DBNull.Value); 

                        if (cmdUpdate.ExecuteNonQuery() == 0) throw new Exception("Erro ao cancelar ou agendamento não encontrado.");
                    }

                    // Libera o horário
                    using (var cmdHorarios = new SqlCommand("UPDATE Horarios SET horarioStatus = 1, idAtend = NULL WHERE idAtend = @AtendimentoId", _connection, transaction))
                    {
                        cmdHorarios.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdHorarios.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return true;
                }
                catch 
                { 
                    transaction.Rollback(); 
                    throw; 
                }
            }
        }

        // Marca como concluído. 
        public bool MarcarAtendimentoConcluido(int atendimentoId)
        {
            string sql = @"UPDATE Atendimentos SET atendStatus = 2, atendDataConclusao = GETDATE() WHERE atendId = @AtendimentoId";
            using(var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // Cancelar Admin.
        // Cancelar Admin (COM MOTIVO)
        public bool CancelarAtendimentoAdmin(int atendimentoId, string motivo)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // Atualiza status para 3 (Cancelado) e grava o motivo na observação
                    string sqlUpdate = @"
                        UPDATE Atendimentos 
                        SET atendStatus = 3, 
                            atendObs = @Motivo 
                        WHERE atendId = @AtendimentoId";

                    using (var cmd = new SqlCommand(sqlUpdate, _connection, transaction)) 
                    {
                        cmd.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmd.Parameters.AddWithValue("@Motivo", (object)motivo ?? DBNull.Value);
                        
                        if (cmd.ExecuteNonQuery() == 0) throw new Exception("Erro ao cancelar. Agendamento não encontrado.");
                    }

                    // Libera o horário na tabela Horarios
                    using (var cmdHorarios = new SqlCommand("UPDATE Horarios SET horarioStatus = 1, idAtend = NULL WHERE idAtend = @AtendimentoId", _connection, transaction)) 
                    {
                        cmdHorarios.Parameters.AddWithValue("@AtendimentoId", atendimentoId);
                        cmdHorarios.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return true;
                }
                catch 
                { 
                    transaction.Rollback(); 
                    throw; 
                }
            }
        }
        

        // Busca por ID.
        public Atendimento ObterAtendimentoPorId(int id)
        {
            string sql = @"
                SELECT a.*, 
                       c.clienteTelefone, uc.usuarioNome as ClienteNome, uc.usuarioEmail as ClienteEmail,
                       ucol.usuarioNome as ColaboradorNome,
                       s.servicoId, s.servicoDesc, s.servicoPreco, s.servicoDuracao, s.servicoStatus
                FROM Atendimentos a
                JOIN Clientes c ON a.idCliente = c.clienteId
                JOIN Usuarios uc ON c.clienteId = uc.usuarioId
                JOIN Colaboradores col ON a.idColab = col.colabId
                JOIN Usuarios ucol ON col.colabId = ucol.usuarioId
                JOIN Servicos s ON a.idServico = s.servicoId 
                WHERE a.atendId = @id";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapReaderToAtendimento(reader);
                    }
                } 
            }
            return null;
        }

        // Busca por Cliente.
        public IEnumerable<Atendimento> ObterAtendimentosPorCliente(int clienteId)
        {
            var lista = new List<Atendimento>();
            
            string sql = @"
                SELECT 
                    a.atendId, a.atendStatus, a.atendDataAgend, a.atendDataAtend, a.atendDataConclusao, a.atendPrecoFinal, a.idColab, a.atendObs,
                    a.idCliente, 
                    a.pagData, 
                    a.pagStatus,
                    ucol.usuarioNome as ColaboradorNome,
                    s.servicoId, s.servicoDesc, s.servicoPreco, s.servicoDuracao, s.servicoStatus
                FROM Atendimentos a
                INNER JOIN Colaboradores col ON a.idColab = col.colabId
                INNER JOIN Usuarios ucol ON col.colabId = ucol.usuarioId
                INNER JOIN Servicos s ON a.idServico = s.servicoId
                WHERE a.idCliente = @clienteId
                ORDER BY a.atendDataAtend DESC";
            
            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@clienteId", clienteId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(MapReaderToAtendimento(reader));
                    }
                }
            }
            return lista;
        }

        // Busca TODOS os atendimentos do sistema (Admin).
        public IEnumerable<Atendimento> ObterTodosAtendimentos()
        {
            var lista = new List<Atendimento>();

            string sql = @"
                SELECT 
                    a.atendId, a.atendStatus, a.atendDataAgend, a.atendDataAtend, a.atendDataConclusao, a.atendPrecoFinal, a.idColab, a.atendObs,
                    a.idCliente, 
                    a.pagData, 
                    a.pagStatus,
                    c.clienteId, uc.usuarioNome as ClienteNome,
                    ucol.usuarioNome as ColaboradorNome,
                    s.servicoId, s.servicoDesc, s.servicoPreco, s.servicoDuracao, s.servicoStatus
                FROM Atendimentos a
                INNER JOIN Clientes c ON a.idCliente = c.clienteId
                INNER JOIN Usuarios uc ON c.clienteId = uc.usuarioId
                INNER JOIN Colaboradores col ON a.idColab = col.colabId
                INNER JOIN Usuarios ucol ON col.colabId = ucol.usuarioId
                INNER JOIN Servicos s ON a.idServico = s.servicoId
                ORDER BY a.atendDataAtend DESC";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(MapReaderToAtendimento(reader));
                    }
                }
            }
            return lista;
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
            var atendimento = new Atendimento
            {
                AtendId = (int)reader["atendId"],
                AtendStatus = (int)reader["atendStatus"],
                AtendDataAgend = (DateTime)reader["atendDataAgend"],
                AtendDataAtend = (DateTime)reader["atendDataAtend"],
                atendDataConclusao = reader.HasColumn("atendDataConclusao") ? reader["atendDataConclusao"] as DateTime? : null,
                AtendObs = reader["atendObs"].ToString(),
                AtendPrecoFinal = reader["atendPrecoFinal"] as decimal?,
                IdCliente = (int)reader["idCliente"],
                IdColab = (int)reader["idColab"],
                PagData = (DateTime)reader["pagData"],
                PagStatus = (int)reader["pagStatus"],
                
                Servicos = new List<Servico> { MapReaderToServico(reader) }
            };
            
            if (reader.HasColumn("ClienteNome"))
            {
                atendimento.Cliente = new Cliente { 
                    ClienteId = (int)reader["idCliente"], 
                    Usuario = new Usuario { UsuarioNome = reader["ClienteNome"].ToString() } 
                };
            }
            if (reader.HasColumn("ColaboradorNome"))
            {
                atendimento.Colaborador = new Colaborador { 
                    ColabId = (int)reader["idColab"], 
                    Usuario = new Usuario { UsuarioNome = reader["ColaboradorNome"].ToString() } 
                };
            }

            return atendimento;
        }
    }
}