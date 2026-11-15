
namespace CadastroProdutoWpf
{
    internal class Setor
    {
        public int IdSetor { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }

        public bool ValidarCadastro()
        {
            return !string.IsNullOrWhiteSpace(Nome);
        }

        public override string ToString() => Nome;
    }
}
