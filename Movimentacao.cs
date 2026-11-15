
using System;

namespace CadastroProdutoWpf
{
    internal enum TipoMovimentacao
    {
        ENTRADA,
        SAIDA,
        TRANSFERENCIA
    }

    internal class Movimentacao
    {
        public int IdMovimentacao { get; set; }

        public TipoMovimentacao Tipo { get; set; }

        public decimal Quantidade { get; set; }

        public DateTime DataMovimentacao { get; set; }

        public int IdProdutoSetor { get; set; }

        public int? IdProdutoSetorDestino { get; set; }

        public int? IdLote { get; set; }

        public int IdUsuario { get; set; }

        public void Validar()
        {
            if (!Enum.IsDefined(typeof(TipoMovimentacao), Tipo))
                throw new ArgumentException("Tipo de movimentação inválido.");

            // Os saldos são armazenados em DECIMAL(12,2) no MySQL.
            if (Quantidade != decimal.Round(Quantidade, 2))
                throw new ArgumentException("A quantidade deve ter no máximo duas casas decimais.");

            if (Quantidade > 9999999999.99m)
                throw new ArgumentException("Quantidade excede o limite do estoque.");

            if (Quantidade <= 0)
                throw new ArgumentException(
                    "A quantidade deve ser maior que zero.");

            if (IdProdutoSetor <= 0 || IdUsuario <= 0)
                throw new ArgumentException(
                    "Produto, setor e usuário são obrigatórios.");

            if (Tipo == TipoMovimentacao.TRANSFERENCIA &&
                (!IdProdutoSetorDestino.HasValue ||
                 IdProdutoSetorDestino.Value <= 0 ||
                 IdProdutoSetorDestino.Value == IdProdutoSetor))
            {
                throw new ArgumentException(
                    "Informe um setor de destino diferente da origem.");
            }

            if (Tipo != TipoMovimentacao.TRANSFERENCIA &&
                IdProdutoSetorDestino.HasValue)
            {
                throw new ArgumentException(
                    "Destino só é permitido em transferências.");
            }
        }
    }
}
