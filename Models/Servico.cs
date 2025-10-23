// Models/Servico.cs
public class Servico
{
    public int Id { get; set; } // Chave Primária
    public string Nome { get; set; } 
    public string Descricao { get; set; }
    public decimal Preco { get; set; }
    public int DuracaoEmMinutos { get; set; } // Essencial para calcular o horário
}