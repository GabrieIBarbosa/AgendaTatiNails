namespace AgendaTatiNails.Models.ViewModels
{
    public class ServicoFaturadoViewModel
    {
        
        public string NomeServico { get; set; } = "Desconhecido";
        
        public int Quantidade { get; set; }
        
       
        public decimal ValorTotal { get; set; }
    }
}