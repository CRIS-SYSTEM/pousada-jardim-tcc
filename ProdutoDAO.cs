
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class ProdutoDAO
    {
        // =========================================
        // CONFIGURAÇÃO DA CONEXÃO COM MYSQL
        // =========================================

        private MySqlConnection CriarConexao() => ConexaoFactory.CriarConexao();

        // =========================================
        // CADASTRAR PRODUTO
        // =========================================

        public void CadastrarProduto(Produto produto)
        {
            if (!produto.ValidarCadastro())
            {
                throw new ArgumentException(
                    "Verifique os dados do produto.");
            }

            string sql = @"
                INSERT INTO produto
                (
                    nome,
                    preco,
                    quantidade,
                    unidade_medida,
                    data_vencimento,
                    situacao,
                    id_categoria
                )
                VALUES
                (
                    @nome,
                    @preco,
                    0,
                    @unidade,
                    NULL,
                    @situacao,
                    @categoria
                )";

            using var conexao = CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            AdicionarParametros(comando, produto);

            comando.ExecuteNonQuery();

            // Recupera o ID gerado pelo MySQL.
            produto.IdProduto =
                Convert.ToInt32(comando.LastInsertedId);
        }


        // =========================================
        // CONSULTAR TODOS OS PRODUTOS
        // =========================================

        public List<Produto> ListarProdutos()
        {
            List<Produto> produtos = new();

            string sql = @"
                SELECT
                    id_produto,
                    nome,
                    preco,
                    quantidade,
                    unidade_medida,
                    data_vencimento,
                    situacao,
                    id_categoria
                FROM produto
                ORDER BY nome";

            using var conexao = CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);
            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                produtos.Add(MapearProduto(leitor));
            }

            return produtos;
        }


        // =========================================
        // CONSULTAR PRODUTO PELO ID
        // =========================================

        public Produto? BuscarPorId(int idProduto)
        {
            string sql = @"
                SELECT
                    id_produto,
                    nome,
                    preco,
                    quantidade,
                    unidade_medida,
                    data_vencimento,
                    situacao,
                    id_categoria
                FROM produto
                WHERE id_produto = @id";

            using var conexao = CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            comando.Parameters.AddWithValue(
                "@id", idProduto);

            using var leitor = comando.ExecuteReader();

            if (leitor.Read())
            {
                return MapearProduto(leitor);
            }

            return null;
        }


        // =========================================
        // ATUALIZAR PRODUTO
        // =========================================

        public bool AtualizarProduto(Produto produto)
        {
            if (!produto.ValidarCadastro())
            {
                throw new ArgumentException(
                    "Verifique os dados do produto.");
            }

            if (produto.IdProduto <= 0)
            {
                throw new ArgumentException(
                    "Informe um ID de produto válido.");
            }

            string sql = @"
                UPDATE produto
                SET
                    nome = @nome,
                    preco = @preco,
                    unidade_medida = @unidade,
                    situacao = @situacao,
                    id_categoria = @categoria
                WHERE id_produto = @id";

            using var conexao = CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            AdicionarParametros(comando, produto);

            comando.Parameters.AddWithValue(
                "@id", produto.IdProduto);

            return comando.ExecuteNonQuery() > 0;
        }


        // =========================================
        // INATIVAR PRODUTO
        // =========================================

        public bool InativarProduto(int idProduto)
        {
            string sql = @"
                UPDATE produto
                SET situacao = FALSE
                WHERE id_produto = @id";

            using var conexao = CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);

            comando.Parameters.AddWithValue(
                "@id", idProduto);

            return comando.ExecuteNonQuery() > 0;
        }


        // =========================================
        // PARÂMETROS UTILIZADOS PELOS COMANDOS
        // =========================================

        private void AdicionarParametros(
            MySqlCommand comando,
            Produto produto)
        {
            comando.Parameters.AddWithValue(
                "@nome", produto.Nome);

            comando.Parameters.AddWithValue(
                "@preco", produto.Preco);

            comando.Parameters.AddWithValue(
                "@unidade", produto.UnidadeMedida);

            comando.Parameters.AddWithValue(
                "@situacao", produto.Situacao);

            comando.Parameters.AddWithValue(
                "@categoria",
                (object?)produto.IdCategoria ?? DBNull.Value);
        }


        // =========================================
        // CONVERTER DADOS DO MYSQL EM OBJETO C#
        // =========================================

        private Produto MapearProduto(MySqlDataReader leitor)
        {
            Produto produto = new Produto
            {
                IdProduto = leitor.GetInt32("id_produto"),

                Nome = leitor.GetString("nome"),

                Preco = leitor.GetDouble("preco"),

                Quantidade = leitor.GetInt32("quantidade"),

                UnidadeMedida =
                    leitor.GetString("unidade_medida"),

                Situacao = leitor.GetBoolean("situacao")
            };

            // Data de vencimento opcional.
            int indiceVencimento =
                leitor.GetOrdinal("data_vencimento");

            if (!leitor.IsDBNull(indiceVencimento))
            {
                produto.DataVencimento =
                    leitor.GetDateTime(indiceVencimento);
            }

            // Categoria opcional durante a migração.
            int indiceCategoria =
                leitor.GetOrdinal("id_categoria");

            if (!leitor.IsDBNull(indiceCategoria))
            {
                produto.IdCategoria =
                    leitor.GetInt32(indiceCategoria);
            }

            return produto;
        }
    }
}
