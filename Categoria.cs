
using System;

namespace CadastroProdutoWpf
{
    internal class Categoria
    {
        // Identificador da categoria.
        public int IdCategoria { get; set; }

        // Nome da categoria.
        public string Nome { get; set; } = string.Empty;

        // Descrição opcional.
        public string? Descricao { get; set; }

        // Construtor padrão.
        public Categoria()
        {
        }

        // Construtor com nome e descrição.
        public Categoria(string nome, string? descricao)
        {
            Nome = nome;
            Descricao = descricao;
        }

        // Validação dos dados.
        public bool ValidarCadastro()
        {
            return !string.IsNullOrWhiteSpace(Nome);
        }

        // Exibição em ComboBox e outras listas.
        public override string ToString()
        {
            return Nome;
        }
    }
}
