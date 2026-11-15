
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class LoteProdutoDAO
    {
        public int Cadastrar(LoteProduto lote)
        {
            if (lote.IdProduto <= 0)
                throw new ArgumentException(
                    "Produto inválido.");

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                INSERT INTO lote_produto
                    (id_produto, numero_lote, data_vencimento)
                VALUES
                    (@produto, @numero, @vencimento)";

            using var comando = new MySqlCommand(sql, conexao);

            comando.Parameters.AddWithValue(
                "@produto", lote.IdProduto);

            comando.Parameters.AddWithValue(
                "@numero",
                string.IsNullOrWhiteSpace(lote.NumeroLote)
                    ? DBNull.Value
                    : lote.NumeroLote);

            comando.Parameters.AddWithValue(
                "@vencimento",
                (object?)lote.DataVencimento ?? DBNull.Value);

            comando.ExecuteNonQuery();

            lote.IdLote = Convert.ToInt32(
                comando.LastInsertedId);

            return lote.IdLote;
        }

        public List<LoteProduto> ListarPorProduto(int idProduto)
        {
            var lista = new List<LoteProduto>();

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                SELECT id_lote, id_produto,
                       numero_lote, data_vencimento
                FROM lote_produto
                WHERE id_produto = @produto
                ORDER BY
                    data_vencimento IS NULL,
                    data_vencimento,
                    id_lote";

            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@produto", idProduto);

            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                lista.Add(new LoteProduto
                {
                    IdLote = leitor.GetInt32("id_lote"),
                    IdProduto = leitor.GetInt32("id_produto"),
                    NumeroLote = leitor.IsDBNull(
                        leitor.GetOrdinal("numero_lote"))
                        ? null
                        : leitor.GetString("numero_lote"),
                    DataVencimento = leitor.IsDBNull(
                        leitor.GetOrdinal("data_vencimento"))
                        ? null
                        : leitor.GetDateTime("data_vencimento")
                });
            }

            return lista;
        }
    }
}
