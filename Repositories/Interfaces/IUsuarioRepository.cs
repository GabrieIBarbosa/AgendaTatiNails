using AgendaTatiNails.Models;

namespace AgendaTatiNails.Repositories.Interfaces
{
    public interface IUsuarioRepository
    {
        Usuario ObterUsuarioPorEmail(string email);
        Cliente ObterClientePorId(int id);
        Cliente AdicionarNovoCliente(Cliente novoCliente);
    }
}