using System.ComponentModel.DataAnnotations;

namespace AgendaTatiNails.Models.ViewModels
{
    // Este ViewModel é usado para exibir o resumo do faturamento por serviço
    public class ServicoFaturadoViewModel
    {
        public string ServicoNome { get; set; } = "Desconhecido";
        public int Quantidade { get; set; }
        public decimal Total { get; set; }
    }
}