using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.Models.ViewModels
{
    
    public class CriarAtendimentoViewModel
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int HorarioId { get; set; } // O ID do 'slot' de Horario escolhido

        [Required]
        [Range(1, int.MaxValue)]
        public int ServicoId { get; set; } // O ID do serviço ÚNICO escolhido
    }
}