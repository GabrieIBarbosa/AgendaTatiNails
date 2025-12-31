using AgendaTatiNails.Models;

namespace AgendaTatiNails.Repositories.Interfaces
{
    public interface IServicoRepository
    {
        Servico ObterServicoPorId(int id);
        IEnumerable<Servico> ObterTodosServicos();
    }
}