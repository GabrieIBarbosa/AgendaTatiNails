namespace AgendaTatiNails.Models
{
    // Mapeia a tabela Servicos
    public class Servico
    {
        public int ServicoId { get; set; }
        public string ServicoDesc { get; set; }
        public int ServicoDuracao { get; set; }
        public decimal ServicoPreco { get; set; }
        public int ServicoStatus { get; set; }
    }
}