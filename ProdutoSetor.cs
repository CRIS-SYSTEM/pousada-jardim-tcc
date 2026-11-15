
using System;

namespace CadastroProdutoWpf
{
    internal class ProdutoSetor
    {
        public int IdProdutoSetor { get; set; }
        public int IdProduto { get; set; }
        public int IdSetor { get; set; }

        public decimal QuantidadeDisponivel { get; set; }
        public decimal? EstoqueMinimo { get; set; }

        public bool EstaAbaixoDoMinimo()
        {
            return EstoqueMinimo.HasValue &&
                   QuantidadeDisponivel <= EstoqueMinimo.Value;
        }

        public void Validar()
        {
            if (IdProduto <= 0 || IdSetor <= 0)
                throw new ArgumentException(
                    "Produto e setor são obrigatórios.");

            if (QuantidadeDisponivel < 0)
                throw new ArgumentException(
                    "Quantidade não pode ser negativa.");

            if (EstoqueMinimo < 0)
                throw new ArgumentException(
                    "Estoque mínimo não pode ser negativo.");
        }
    }
}
