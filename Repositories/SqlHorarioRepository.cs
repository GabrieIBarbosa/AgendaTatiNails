using AgendaTatiNails.Models;
using AgendaTatiNails.Repositories.Interfaces; // <-- Importa a nova interface
using Microsoft.Data.SqlClient;
using System.Data;

namespace AgendaTatiNails.Repositories
{
    public class SqlHorarioRepository : DbConnection, IHorarioRepository // <-- Implementa a nova interface
    {
        public SqlHorarioRepository(IConfiguration configuration)
            : base(configuration.GetConnectionString("DefaultConnection"))
        {
            // Conexão aberta na classe base
        }

        // (Resumo) Busca o primeiro slot de horário associado a um atendimento (usado na página 'Editar').
        public Horario ObterHorarioPorAtendimentoId(int atendimentoId)
        {
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
            return null; 
        }

        // (Resumo) Busca os horários disponíveis, garantindo que os slots existam (chama 'sp_GarantirHorariosParaData') e filtrando por slots consecutivos (lógica do 'WITH SlotsComJanela...').
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
                // A query simples para 45min
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
                            SUM(CASE WHEN horarioStatus != 1 THEN 1 ELSE 0 END) 
                                OVER (
                                    ORDER BY horarioPeriodo 
                                    ROWS BETWEEN CURRENT ROW AND {slotsNecessarios - 1} FOLLOWING
                                ) as SlotsOcupadosNaJanela,
                            LEAD(horarioPeriodo, {slotsNecessarios - 1}) 
                                OVER (
                                    ORDER BY horarioPeriodo
                                ) as PeriodoFinalDaJanela
                        FROM Horarios
                        WHERE horarioData = @data
                    )
                    SELECT * FROM SlotsComJanela
                    WHERE 
                        SlotsOcupadosNaJanela = 0 
                        AND PeriodoFinalDaJanela IS NOT NULL 
                        AND DATEDIFF(minute, horarioPeriodo, PeriodoFinalDaJanela) = {(slotsNecessarios - 1) * 45} 
                        AND DATEADD(day, DATEDIFF(day, 0, horarioData), CAST(horarioPeriodo AS DATETIME)) > GETDATE()
                    ORDER BY horarioPeriodo;
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
        public void BloquearHorario(int horarioId)
        {
            // Só bloqueia se estiver Livre (1). Não sobrescreve agendamentos (2).
            string sql = "UPDATE Horarios SET horarioStatus = 3 WHERE horarioId = @id AND horarioStatus = 1";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@id", horarioId);
                cmd.ExecuteNonQuery();
            }
        }

        public void DesbloquearHorario(int horarioId)
        {
            // Retorna para Livre (1) apenas se estiver Bloqueado (3).
            string sql = "UPDATE Horarios SET horarioStatus = 1 WHERE horarioId = @id AND horarioStatus = 3";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@id", horarioId);
                cmd.ExecuteNonQuery();
            }
        }

        public void BloquearDiaInteiro(DateTime data)
        {
            // PASSO 1: Garantir que os horários existam no banco
            // Se o admin bloquear o Natal de 2025 hoje, a SP precisa criar os slots vazios primeiro.
            using (var cmd = new SqlCommand("sp_GarantirHorariosParaData", _connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Data", data.Date);
                cmd.ExecuteNonQuery();
            }

            // PASSO 2: Bloquear tudo que estiver livre naquele dia
            string sqlUpdate = @"
                UPDATE Horarios 
                SET horarioStatus = 3 
                WHERE horarioData = @Data AND horarioStatus = 1";

            using (var cmd = new SqlCommand(sqlUpdate, _connection))
            {
                cmd.Parameters.AddWithValue("@Data", data.Date);
                cmd.ExecuteNonQuery();
            }
        }

        // Método auxiliar para o Admin ver a agenda completa (inclusive bloqueados e agendados)
        public IEnumerable<Horario> ObterTodosHorariosDoDia(DateTime data)
        {
            // Primeiro garante que existem
            using (var cmd = new SqlCommand("sp_GarantirHorariosParaData", _connection))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Data", data.Date);
                cmd.ExecuteNonQuery();
            }

            var lista = new List<Horario>();
            string sql = "SELECT * FROM Horarios WHERE horarioData = @Data ORDER BY horarioPeriodo";

            using (var cmd = new SqlCommand(sql, _connection))
            {
                cmd.Parameters.AddWithValue("@Data", data.Date);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new Horario
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
            return lista;
        }
    }
        
}