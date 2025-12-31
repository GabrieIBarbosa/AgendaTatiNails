using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.Models.ViewModels
{
    public class EditarAgendamentoViewModel
    {
        // --- Propriedades para o POST (Estas SIM são obrigatórias) ---
        public int AtendimentoId { get; set; } 

        [Required(ErrorMessage = "Selecione o novo horário.")]
        [Range(1, int.MaxValue, ErrorMessage = "Selecione um horário válido.")]
        public int HorarioId { get; set; } 

        [Required(ErrorMessage = "Selecione o novo serviço.")]
        [Range(1, int.MaxValue, ErrorMessage = "Selecione um serviço válido.")]
        public int ServicoId { get; set; } 
        
        // --- ALTERAÇÃO AQUI: Adicionei o '?' para tornar Opcional ---
        public string? Observacao { get; set; }

        
        // --- Propriedades para o GET (Marcadas como NULÁVEIS '?') ---
        // O [BindNever] garante que o Controller não tente validar isso no POST

        [BindNever]
        public Atendimento? AtendimentoAtual { get; set; } 

        [BindNever]
        public Horario? HorarioAtual { get; set; } 

        [BindNever]
        public SelectList? TodosOsServicos { get; set; } 
    }
}