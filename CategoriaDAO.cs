
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class CategoriaDAO
    {
        public List<Categoria> Listar()
        {
            var categorias = new List<Categoria>();

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                SELECT id_categoria, nome, descricao
                FROM categoria
                ORDER BY nome";

            using var comando = new MySqlCommand(sql, conexao);
            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                categorias.Add(new Categoria
                {
                    IdCategoria = leitor.GetInt32("id_categoria"),
                    Nome = leitor.GetString("nome"),
                    Descricao = leitor.IsDBNull(
                        leitor.GetOrdinal("descricao"))
                        ? null
                        : leitor.GetString("descricao")
                });
            }

            return categorias;
        }

        public int Cadastrar(Categoria item)
        {
            Validar(item);
            const string sql = "INSERT INTO categoria (nome, descricao) VALUES (@nome, @descricao)";
            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();
            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@nome", item.Nome.Trim());
            comando.Parameters.AddWithValue("@descricao", string.IsNullOrWhiteSpace(item.Descricao) ? DBNull.Value : item.Descricao.Trim());
            comando.ExecuteNonQuery();
            return Convert.ToInt32(comando.LastInsertedId);
        }

        public void Atualizar(Categoria item)
        {
            Validar(item);
            if (item.IdCategoria <= 0)
                throw new ArgumentException("Selecione um registro válido.");
            const string sql = "UPDATE categoria SET nome = @nome, descricao = @descricao WHERE id_categoria = @id";
            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();
            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@id", item.IdCategoria);
            comando.Parameters.AddWithValue("@nome", item.Nome.Trim());
            comando.Parameters.AddWithValue("@descricao", string.IsNullOrWhiteSpace(item.Descricao) ? DBNull.Value : item.Descricao.Trim());
            comando.ExecuteNonQuery();
        }

        private static void Validar(Categoria item)
        {
            if (string.IsNullOrWhiteSpace(item.Nome))
                throw new ArgumentException("O nome é obrigatório.");
            if (item.Nome.Trim().Length > 100)
                throw new ArgumentException("O nome deve ter no máximo 100 caracteres.");
            if (item.Descricao?.Length > 255)
                throw new ArgumentException("A descrição deve ter no máximo 255 caracteres.");
        }
    }
}
