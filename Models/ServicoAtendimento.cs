namespace AgendaTatiNails.Models
{
    // Mapeia a tabela de junção ServicosAtend
    public class ServicoAtendimento
    {
        public decimal? SaDesconto { get; set; }
        public int IdAtend { get; set; }
        public int IdServico { get; set; }
    }
}