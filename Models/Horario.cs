using System;

namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Horarios
    public class Horario
    {
        public int HorarioId { get; set; }
        public int HorarioStatus { get; set; }
        
        public TimeSpan HorarioPeriodo { get; set; } 

        public DateTime HorarioData { get; set; }
        public int? IdAtend { get; set; } // Nullable
    }
}