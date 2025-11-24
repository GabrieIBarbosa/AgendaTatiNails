using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.Models.ViewModels
{
    public class CriarAtendimentoViewModel
    {
        [Required]
        public int HorarioId { get; set; }

        [Required(ErrorMessage = "Selecione um serviço.")]
        [Range(1, int.MaxValue, ErrorMessage = "Serviço inválido.")]
        public int ServicoId { get; set; } 
        public string Observacao { get; set; }
    }
}