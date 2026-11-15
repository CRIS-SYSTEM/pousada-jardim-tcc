
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class ProdutoSetorDAO
    {
        public List<ProdutoSetor> Listar()
        {
            var lista = new List<ProdutoSetor>();

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                SELECT id_produto_setor,
                       id_produto,
                       id_setor,
                       quantidade_disponivel,
                       estoque_minimo
                FROM produto_setor
                ORDER BY id_setor, id_produto";

            using var comando = new MySqlCommand(sql, conexao);
            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                lista.Add(new ProdutoSetor
                {
                    IdProdutoSetor =
                        leitor.GetInt32("id_produto_setor"),
                    IdProduto = leitor.GetInt32("id_produto"),
                    IdSetor = leitor.GetInt32("id_setor"),
                    QuantidadeDisponivel =
                        leitor.GetDecimal("quantidade_disponivel"),
                    EstoqueMinimo = leitor.IsDBNull(
                        leitor.GetOrdinal("estoque_minimo"))
                        ? null
                        : leitor.GetDecimal("estoque_minimo")
                });
            }

            return lista;
        }

        public void Vincular(int idProduto, int idSetor)
        {
            if (idProduto <= 0 || idSetor <= 0)
                throw new ArgumentException(
                    "Produto e setor devem ser válidos.");

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                INSERT INTO produto_setor
                    (id_produto, id_setor)
                VALUES
                    (@produto, @setor)
                ON DUPLICATE KEY UPDATE
                    id_produto_setor = id_produto_setor";

            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@produto", idProduto);
            comando.Parameters.AddWithValue("@setor", idSetor);
            comando.ExecuteNonQuery();
        }

        public bool DefinirEstoqueMinimo(
            int idProdutoSetor,
            decimal? minimo)
        {
            if (idProdutoSetor <= 0 || minimo < 0)
                throw new ArgumentException(
                    "Dados inválidos para estoque mínimo.");

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                UPDATE produto_setor
                SET estoque_minimo = @minimo
                WHERE id_produto_setor = @id";

            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue(
                "@minimo", (object?)minimo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@id", idProdutoSetor);

            return comando.ExecuteNonQuery() > 0;
        }
    }
}
