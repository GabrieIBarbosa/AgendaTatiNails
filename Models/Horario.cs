using System;

namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Horarios
    public class Horario
    {
        public int HorarioId { get; set; }
        // 1 = Livre, 2 = Agendado, 3 = Bloqueado pelo Admin
        public int HorarioStatus { get; set; }
        
        public TimeSpan HorarioPeriodo { get; set; } 

        public DateTime HorarioData { get; set; }
        public int? IdAtend { get; set; } // Nullable
    }
}