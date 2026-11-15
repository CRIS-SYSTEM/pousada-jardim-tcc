
using System;
using System.Globalization;

namespace CadastroProdutoWpf
{
    internal class Produto
    {
        // =====================================
        // ATRIBUTOS DO PRODUTO
        // =====================================

        // Identificador do produto no banco.
        public int IdProduto { get; set; }

        // Nome do produto.
        public string Nome = string.Empty;

        // Preço unitário do produto.
        public double Preco;

        // Campo legado mantido apenas para migração assistida.
        // NÃO representa o saldo operacional, controlado por setor e lote.
        public int Quantidade;

        // Unidade de medida: UN, KG, L, ML etc.
        public string UnidadeMedida { get; set; } = "UN";

        // Campo legado de vencimento; novos vencimentos ficam em lote_produto.
        public DateTime? DataVencimento { get; set; }

        // TRUE = Ativo.
        // FALSE = Inativo.
        public bool Situacao { get; set; } = true;

        // Identificador da categoria do produto.
        // Nullable durante a migração dos dados.
        public int? IdCategoria { get; set; }


        // =====================================
        // CONSTRUTORES
        // =====================================

        // Construtor padrão.
        public Produto()
        {
        }

        // Construtor com nome, preço e quantidade.
        public Produto(
            string nome,
            double preco,
            int quantidade)
        {
            Nome = nome;
            Preco = preco;
            Quantidade = quantidade;
        }

        // Construtor com nome e preço.
        public Produto(string nome, double preco)
        {
            Nome = nome;
            Preco = preco;
            Quantidade = 0;
        }


        // =====================================
        // VALIDAÇÃO DO CADASTRO
        // =====================================

        public bool ValidarCadastro()
        {
            // O nome não pode estar vazio.
            if (string.IsNullOrWhiteSpace(Nome))
                return false;

            // O preço não pode ser negativo.
            if (double.IsNaN(Preco) || double.IsInfinity(Preco) || Preco < 0)
                return false;

            // A quantidade não pode ser negativa.
            if (Quantidade < 0)
                return false;

            // A unidade de medida é obrigatória.
            if (string.IsNullOrWhiteSpace(UnidadeMedida))
                return false;

            // A categoria será obrigatória após
            // a migração dos produtos existentes.

            return true;
        }


        // =====================================
        // CONTROLE TEMPORÁRIO DE ESTOQUE
        // =====================================

        // Calcula o valor total do estoque.
        public double ValorTotalEmEstoque()
        {
            return Preco * Quantidade;
        }

        // Adiciona quantidade ao estoque.
        public void AdicionarProdutos(int quantidade)
        {
            if (quantidade <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            Quantidade += quantidade;
        }

        // Remove quantidade do estoque.
        public void RemoverProdutos(int quantidade)
        {
            if (quantidade <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            if (quantidade > Quantidade)
                throw new InvalidOperationException(
                    "Quantidade insuficiente em estoque.");

            Quantidade -= quantidade;
        }


        // =====================================
        // INATIVAÇÃO DO PRODUTO
        // =====================================

        public void Inativar()
        {
            Situacao = false;
        }


        // =====================================
        // EXIBIÇÃO DOS DADOS
        // =====================================

        public override string ToString()
        {
            string vencimento =
                DataVencimento.HasValue
                ? DataVencimento.Value.ToString("dd/MM/yyyy")
                : "Não aplicável";

            string situacao =
                Situacao ? "Ativo" : "Inativo";

            return
                $"Produto: {Nome}\n" +
                $"Preço: {Preco.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))}\n" +
                $"Quantidade: {Quantidade} {UnidadeMedida}\n" +
                $"Vencimento: {vencimento}\n" +
                $"Situação: {situacao}\n" +
                $"Valor total: {ValorTotalEmEstoque().ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))}";
        }
    }
}
