
using System;

namespace CadastroProdutoWpf
{
    internal class LoteProduto
    {
        public int IdLote { get; set; }
        public int IdProduto { get; set; }
        public string? NumeroLote { get; set; }
        public DateTime? DataVencimento { get; set; }

        public bool EstaVencido()
        {
            return DataVencimento.HasValue &&
                   DataVencimento.Value.Date < DateTime.Today;
        }

        public bool VenceEmAte(int dias)
        {
            if (dias < 0)
                throw new ArgumentException(
                    "Dias não pode ser negativo.");

            return DataVencimento.HasValue &&
                   DataVencimento.Value.Date >= DateTime.Today &&
                   DataVencimento.Value.Date <=
                       DateTime.Today.AddDays(dias);
        }
    }
}
