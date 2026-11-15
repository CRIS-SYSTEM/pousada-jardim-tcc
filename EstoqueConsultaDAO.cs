
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class EstoqueResumo
    {
        public int IdProduto { get; set; }
        public string Produto { get; set; } = "";
        public string Setor { get; set; } = "";
        public decimal Quantidade { get; set; }
        public decimal? EstoqueMinimo { get; set; }

        public string Situacao =>
            !EstoqueMinimo.HasValue
                ? "Não configurado"
                : Quantidade <= EstoqueMinimo.Value
                    ? "Abaixo ou no mínimo"
                    : "Normal";
    }

    internal class EstoqueConsultaDAO
    {
        public List<EstoqueResumo> ListarPorSetor()
        {
            var lista = new List<EstoqueResumo>();

            const string sql = @"
                SELECT
                    p.id_produto,
                    p.nome AS produto,
                    s.nome AS setor,
                    ps.quantidade_disponivel,
                    ps.estoque_minimo
                FROM produto_setor ps
                INNER JOIN produto p
                    ON p.id_produto = ps.id_produto
                INNER JOIN setor s
                    ON s.id_setor = ps.id_setor
                ORDER BY s.nome, p.nome";

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);
            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                lista.Add(new EstoqueResumo
                {
                    IdProduto = leitor.GetInt32("id_produto"),
                    Produto = leitor.GetString("produto"),
                    Setor = leitor.GetString("setor"),
                    Quantidade = leitor.GetDecimal(
                        "quantidade_disponivel"),
                    EstoqueMinimo = leitor.IsDBNull(
                        leitor.GetOrdinal("estoque_minimo"))
                        ? null
                        : leitor.GetDecimal("estoque_minimo")
                });
            }

            return lista;
        }

        public int ContarAbaixoDoMinimo()
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM produto_setor
                WHERE estoque_minimo IS NOT NULL
                  AND quantidade_disponivel <= estoque_minimo";

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            return Convert.ToInt32(comando.ExecuteScalar());
        }

        public int ContarAcimaDoMinimo()
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM produto_setor
                WHERE estoque_minimo IS NOT NULL
                  AND quantidade_disponivel > estoque_minimo";

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            return Convert.ToInt32(comando.ExecuteScalar());
        }

        public int ContarMovimentacoes()
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM movimentacao";

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            return Convert.ToInt32(comando.ExecuteScalar());
        }
    }
}
